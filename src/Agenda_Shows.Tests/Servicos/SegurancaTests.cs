using Agenda_Shows.Entities;
using Agenda_Shows.Services.Pedidos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Tests.Servicos;

/// <summary>
/// As correções da revisão de segurança de 18/10: teto por pessoa, uma reserva em aberto por evento, evento cancelado
/// (não paga, libera a reserva, ingresso não entra) e o papel lido do BANCO nas ações (sessão antiga de quem foi rebaixado).
/// </summary>
public class SegurancaTests
{
    // A Carla não compra nada na semeadura: começa com zero ingressos em qualquer evento.
    private const string Carla = "carla@umura.dev";

    private static async Task<(int EventoId, TipoDeIngresso Lote)> LoteAsync(BancoDeTeste b, string slug = "rock-autoral-largo-da-ordem")
    {
        await using var db = b.Db();
        var e = await db.Eventos.Include(x => x.Ingressos).SingleAsync(x => x.Slug == slug);
        return (e.Id, e.Ingressos.OrderBy(i => i.Ordem).First(i => i.PrecoCentavos > 0));
    }

    private static async Task<int> VendidosAsync(BancoDeTeste b, int loteId)
    {
        await using var db = b.Db();
        return (await db.TiposDeIngresso.SingleAsync(t => t.Id == loteId)).Vendidos;
    }

    [Fact]
    public async Task UmaReservaEmAbertoPorEvento_ASegundaERecusadaAteAPrimeiraSair()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LoteAsync(b);
        var carla = b.Quem(Carla);

        var primeiro = await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 2)]);
        Assert.True(primeiro.Ok, primeiro.Erro);
        var segundo = await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 1)]);
        Assert.False(segundo.Ok);
        Assert.Contains("esperando pagamento", segundo.Erro);
        Assert.Equal(lote.Vendidos + 2, await VendidosAsync(b, lote.Id));

        await b.Pedidos().CancelarAsync(carla, primeiro.Valor!);
        Assert.True((await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 1)])).Ok);
    }

    [Fact]
    public async Task TetoPorPessoa_SomaOsPedidosPagos()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LoteAsync(b);
        var carla = b.Quem(Carla);

        var seis = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 6)])).Valor!;
        Assert.True((await b.Pedidos().PagarPixSimuladoAsync(carla, seis)).Ok);

        var cinco = await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 5)]);
        Assert.False(cinco.Ok);
        Assert.Contains("mais 4", cinco.Erro);

        var quatro = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 4)])).Valor!;
        Assert.True((await b.Pedidos().PagarPixSimuladoAsync(carla, quatro)).Ok);
        var mais = await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 1)]);
        Assert.False(mais.Ok);
        Assert.Contains("máximo por pessoa", mais.Erro);

        // O teto é por evento: em outro evento a Carla compra normalmente.
        var (outroId, outroLote) = await LoteAsync(b, "galpao-sessions");
        Assert.True((await b.Pedidos().CriarAsync(carla, outroId, [new Escolha(outroLote.Id, 2)])).Ok);
    }

    [Fact]
    public async Task EventoCancelado_CancelaAsReservas_DevolveOLote_ENaoDeixaPagar()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LoteAsync(b); // do Coletivo Cena Livre (cena@)
        var carla = b.Quem(Carla);
        var codigo = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 2)])).Valor!;

        Assert.True((await b.Organizador().CancelarAsync(b.Quem("cena@umura.dev"), eventoId)).Ok);

        var pedido = await b.Pedidos().ObterAsync(carla, codigo);
        Assert.Equal(StatusDoPedido.Cancelado, pedido!.Status);
        Assert.Equal(lote.Vendidos, await VendidosAsync(b, lote.Id));
        var pagar = await b.Pedidos().PagarPixSimuladoAsync(carla, codigo);
        Assert.False(pagar.Ok);
        Assert.Contains("cancelado", pagar.Erro);
    }

    [Fact]
    public async Task Pagamento_ConfereOEvento_MesmoQueAReservaAindaEstejaAberta()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LoteAsync(b);
        var carla = b.Quem(Carla);
        var codigo = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 1)])).Valor!;

        // O evento sai do ar sem passar pelo cancelamento (o organizador voltou para rascunho).
        await using (var db = b.Db())
            await db.Eventos.Where(e => e.Id == eventoId).ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, StatusDoEvento.Rascunho));

        var r = await b.Pedidos().PagarCartaoSimuladoAsync(carla, codigo, Pedidos.CartaoAprovado, "12/30", "123");
        Assert.NotEqual(DesfechoDoCartao.Aprovado, r.Desfecho);
        Assert.Contains("não está mais à venda", r.Mensagem);
        Assert.Empty((await b.Pedidos().ObterAsync(carla, codigo))!.Ingressos);
    }

    [Fact]
    public async Task IngressoDeEventoCancelado_NaoEntra()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = b.Quem("cena@umura.dev");
        var evento = (await b.Organizador().MeusEventosAsync(cena)).First(e => e.Vendidos > 0);
        string codigo;
        await using (var db = b.Db())
            codigo = (await db.Ingressos.FirstAsync(i => i.EventoId == evento.Id && i.UsadoEmUtc == null)).Codigo;

        Assert.True((await b.Organizador().CancelarAsync(cena, evento.Id)).Ok);
        var r = await b.Organizador().ValidarIngressoAsync(cena, evento.Id, codigo);
        Assert.False(r.Ok);
        Assert.Contains("cancelado", r.Erro);
    }

    private static async Task RebaixarAsync(BancoDeTeste b, string email)
    {
        await using var db = b.Db();
        await db.Usuarios.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.Papel, Papel.Usuario));
    }

    [Fact]
    public async Task AdminRebaixado_ComATelaAberta_NaoMudaPapelNemDestaca()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var adminAntigo = b.Quem("admin@umura.dev"); // a foto do papel tirada quando a tela abriu
        int anaId, eventoId;
        await using (var db = b.Db())
        {
            anaId = (await db.Usuarios.SingleAsync(u => u.Email == "ana@umura.dev")).Id;
            eventoId = (await db.Eventos.FirstAsync()).Id;
        }
        await RebaixarAsync(b, "admin@umura.dev");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Contas().MudarPapelAsync(adminAntigo, anaId, Papel.Admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().AlternarDestaqueAsync(adminAntigo, eventoId));
        await using var depois = b.Db();
        Assert.Equal(Papel.Usuario, (await depois.Usuarios.SingleAsync(u => u.Id == anaId)).Papel);
    }

    [Fact]
    public async Task OrganizadorRebaixado_ComATelaAberta_NaoSalvaNemCancelaNemValida()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cenaAntiga = b.Quem("cena@umura.dev");
        var evento = (await b.Organizador().MeusEventosAsync(cenaAntiga)).First(e => e.Vendidos > 0);
        var form = await b.Organizador().ParaEditarAsync(cenaAntiga, evento.Id);
        string codigo;
        await using (var db = b.Db())
            codigo = (await db.Ingressos.FirstAsync(i => i.EventoId == evento.Id && i.UsadoEmUtc == null)).Codigo;
        await RebaixarAsync(b, "cena@umura.dev");

        form!.Titulo = "Editado depois de rebaixada";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().SalvarAsync(cenaAntiga, form));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().CancelarAsync(cenaAntiga, evento.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().ValidarIngressoAsync(cenaAntiga, evento.Id, codigo));

        await using var depois = b.Db();
        var e = await depois.Eventos.SingleAsync(x => x.Id == evento.Id);
        Assert.NotEqual("Editado depois de rebaixada", e.Titulo);
        Assert.Equal(StatusDoEvento.Publicado, e.Status);
    }
}
