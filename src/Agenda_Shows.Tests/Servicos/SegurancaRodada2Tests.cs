using Agenda_Shows.Entities;
using Agenda_Shows.Services.Pedidos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Tests.Servicos;

/// <summary>A 2ª rodada de 26/10: o teto reconferido no pagamento, a senha do admin separada e a receita somada em long.</summary>
public class SegurancaRodada2Tests
{
    private const string Carla = "carla@umura.dev"; // não compra nada na semeadura

    private static async Task<(int EventoId, TipoDeIngresso Lote)> LoteAsync(BancoDeTeste b, string slug = "rock-autoral-largo-da-ordem")
    {
        await using var db = b.Db();
        var e = await db.Eventos.Include(x => x.Ingressos).SingleAsync(x => x.Slug == slug);
        return (e.Id, e.Ingressos.OrderBy(i => i.Ordem).First(i => i.PrecoCentavos > 0));
    }

    private static async Task ExpirarNoBancoAsync(BancoDeTeste b, string codigo, DateTime quando)
    {
        await using var db = b.Db();
        await db.Pedidos.Where(p => p.Codigo == codigo).ExecuteUpdateAsync(s => s.SetProperty(p => p.ExpiraEmUtc, quando));
    }

    [Fact]
    public async Task Pagamento_ReconfereOTeto_DoisPedidosDe6NaoViram12()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LoteAsync(b);
        var carla = b.Quem(Carla);

        var a = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 6)])).Valor!;
        // A "venceu" no relógio (o job ainda não passou): já não conta como reserva em aberto, e B pode nascer.
        await ExpirarNoBancoAsync(b, a, b.Relogio.Agora.AddSeconds(-1));
        var bCod = (await b.Pedidos().CriarAsync(carla, eventoId, [new Escolha(lote.Id, 6)])).Valor!;
        Assert.True((await b.Pedidos().PagarPixSimuladoAsync(carla, bCod)).Ok);

        // A corrida: o pagamento de A chega como se ainda estivesse no prazo (o clique foi no último segundo).
        await ExpirarNoBancoAsync(b, a, b.Relogio.Agora.AddMinutes(5));
        var r = await b.Pedidos().PagarPixSimuladoAsync(carla, a);
        Assert.False(r.Ok);
        Assert.Contains("máximo", r.Erro);
        Assert.Equal(StatusDoPedido.AguardandoPagamento, (await b.Pedidos().ObterAsync(carla, a))!.Status);
    }

    [Fact]
    public async Task SenhaDaDemo_NaoAbreOAdmin_SemASenhaPropria()
    {
        using var b = new BancoDeTeste();
        await b.Semeadura().SemearAsync("demo-compartilhada-1", senhaDoAdmin: null);

        Assert.NotNull(await b.Contas().AutenticarAsync("cena@umura.dev", "demo-compartilhada-1"));
        Assert.NotNull(await b.Contas().AutenticarAsync("ana@umura.dev", "demo-compartilhada-1"));
        Assert.Null(await b.Contas().AutenticarAsync("admin@umura.dev", "demo-compartilhada-1"));
    }

    [Fact]
    public async Task SenhaDoAdmin_SoElaAbreOAdmin()
    {
        using var b = new BancoDeTeste();
        await b.Semeadura().SemearAsync("demo-compartilhada-1", senhaDoAdmin: "so-do-admin-2");

        Assert.NotNull(await b.Contas().AutenticarAsync("admin@umura.dev", "so-do-admin-2"));
        Assert.Null(await b.Contas().AutenticarAsync("admin@umura.dev", "demo-compartilhada-1"));
        Assert.Null(await b.Contas().AutenticarAsync("cena@umura.dev", "so-do-admin-2"));
    }

    [Fact]
    public async Task Receita_PassaDoLimiteDoInt_SemDerrubarOPainel()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        await using (var db = b.Db())
        {
            var ids = await db.Pedidos.Where(p => p.Status == StatusDoPedido.Pago).OrderBy(p => p.Id).Select(p => p.Id).Take(3).ToListAsync();
            await db.Pedidos.Where(p => ids.Contains(p.Id)).ExecuteUpdateAsync(s => s.SetProperty(p => p.TotalCentavos, 1_500_000_000));
        }

        var painel = await b.Admin().PainelAsync(b.Quem("admin@umura.dev"));
        Assert.True(painel.ReceitaCentavos > int.MaxValue);
        var cena = await b.Organizador().PainelAsync(b.Quem("cena@umura.dev")); // os painéis do organizador também somam
        Assert.True(cena.ReceitaCentavos >= 0);
    }
}
