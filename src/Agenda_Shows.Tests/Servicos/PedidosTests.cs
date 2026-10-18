using Agenda_Shows.Entities;
using Agenda_Shows.Services.Pedidos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Tests.Servicos;

public class PedidosTests
{
    private static async Task<(int EventoId, TipoDeIngresso Lote)> LotePagoAsync(BancoDeTeste b, string slug = "rock-autoral-largo-da-ordem")
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
    public async Task Criar_ReservaOsIngressos_EOTotalVemDoBanco()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");

        var r = await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 2)]);

        Assert.True(r.Ok, r.Erro);
        Assert.Equal(lote.Vendidos + 2, await VendidosAsync(b, lote.Id));
        var p = await b.Pedidos().ObterAsync(ana, r.Valor!);
        Assert.Equal(StatusDoPedido.AguardandoPagamento, p!.Status);
        Assert.Equal(lote.PrecoCentavos * 2, p.TotalCentavos);
        Assert.Empty(p.Ingressos);
    }

    [Fact]
    public async Task UltimoIngresso_NaoVendeDuasVezes()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        await using (var db = b.Db())
            await db.TiposDeIngresso.Where(t => t.Id == lote.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Quantidade, t => t.Vendidos + 1));

        var primeiro = await b.Pedidos().CriarAsync(b.Quem("ana@umura.dev"), eventoId, [new Escolha(lote.Id, 1)]);
        var segundo = await b.Pedidos().CriarAsync(b.Quem("bruno@umura.dev"), eventoId, [new Escolha(lote.Id, 1)]);

        Assert.True(primeiro.Ok);
        Assert.False(segundo.Ok);
    }

    [Fact]
    public async Task Pix_PagaEEmiteUmIngressoPorPessoa_EOCliqueDuploNaoDuplica()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");
        var codigo = (await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 3)])).Valor!;

        var r1 = await b.Pedidos().PagarPixSimuladoAsync(ana, codigo);
        var r2 = await b.Pedidos().PagarPixSimuladoAsync(ana, codigo);

        Assert.True(r1.Ok, r1.Erro);
        Assert.True(r2.Ok, r2.Erro);
        Assert.Equal(StatusDoPedido.Pago, r2.Valor!.Status);
        Assert.Equal(3, r2.Valor.Ingressos.Count);
        Assert.Equal(3, r2.Valor.Ingressos.Select(i => i.Codigo).Distinct().Count());
    }

    [Fact]
    public async Task Gratuito_JaNascePagoComIngresso()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        await using var db = b.Db();
        var eventoId = (await db.Eventos.SingleAsync(e => e.Slug == "mpb-no-barigui")).Id;
        var gratis = await db.TiposDeIngresso.FirstAsync(t => t.EventoId == eventoId);
        Assert.Equal(0, gratis.PrecoCentavos);

        var codigo = (await b.Pedidos().CriarAsync(b.Quem("ana@umura.dev"), eventoId, [new Escolha(gratis.Id, 2)])).Valor!;
        var p = await b.Pedidos().ObterAsync(b.Quem("ana@umura.dev"), codigo);
        Assert.Equal(StatusDoPedido.Pago, p!.Status);
        Assert.Equal(MetodoDePagamento.Gratuito, p.Metodo);
        Assert.Equal(2, p.Ingressos.Count);
    }

    [Fact]
    public async Task ReservaVencida_Expira_DevolveOLote_ENaoPodeMaisSerPaga()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");
        var codigo = (await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 2)])).Valor!;

        b.Relogio.Agora += Pedidos.PrazoParaPagar + TimeSpan.FromMinutes(1);
        Assert.Equal(1, await b.Pedidos().ExpirarVencidosAsync());
        Assert.Equal(0, await b.Pedidos().ExpirarVencidosAsync());   // exatamente uma vez

        Assert.Equal(lote.Vendidos, await VendidosAsync(b, lote.Id));
        Assert.False((await b.Pedidos().PagarPixSimuladoAsync(ana, codigo)).Ok);
    }

    [Fact]
    public async Task PagarDepoisDoPrazo_FalhaMesmoAntesDoJobRodar()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");
        var codigo = (await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 1)])).Valor!;

        b.Relogio.Agora += Pedidos.PrazoParaPagar + TimeSpan.FromSeconds(1);
        var r = await b.Pedidos().PagarPixSimuladoAsync(ana, codigo);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task Cancelar_DevolveOLote()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");
        var codigo = (await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 2)])).Valor!;

        Assert.True((await b.Pedidos().CancelarAsync(ana, codigo)).Ok);
        Assert.Equal(lote.Vendidos, await VendidosAsync(b, lote.Id));
    }

    [Theory]
    [InlineData("4242 4242 4242 4242", DesfechoDoCartao.Aprovado)]
    [InlineData("4000 0000 0000 0002", DesfechoDoCartao.Recusado)]
    [InlineData("5555 5555 5555 4444", DesfechoDoCartao.NaoEhCartaoDeTeste)]
    [InlineData("1234 5678 9012 3456", DesfechoDoCartao.Invalido)]
    public async Task Cartao_SoOsDeTesteFuncionam(string numero, DesfechoDoCartao esperado)
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var ana = b.Quem("ana@umura.dev");
        var codigo = (await b.Pedidos().CriarAsync(ana, eventoId, [new Escolha(lote.Id, 1)])).Valor!;

        var (desfecho, _) = await b.Pedidos().PagarCartaoSimuladoAsync(ana, codigo, numero, "12/30", "123");

        Assert.Equal(esperado, desfecho);
        var p = await b.Pedidos().ObterAsync(ana, codigo);
        Assert.Equal(esperado == DesfechoDoCartao.Aprovado ? StatusDoPedido.Pago : StatusDoPedido.AguardandoPagamento, p!.Status);
        if (esperado == DesfechoDoCartao.Aprovado) Assert.Equal("4242", p.CartaoFinal);
    }

    [Fact]
    public async Task PedidoDeOutraPessoa_NaoAparece_MasOAdminVe()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var codigo = (await b.Pedidos().CriarAsync(b.Quem("ana@umura.dev"), eventoId, [new Escolha(lote.Id, 1)])).Valor!;

        Assert.Null(await b.Pedidos().ObterAsync(b.Quem("bruno@umura.dev"), codigo));
        Assert.False((await b.Pedidos().PagarPixSimuladoAsync(b.Quem("bruno@umura.dev"), codigo)).Ok);
        Assert.NotNull(await b.Pedidos().ObterAsync(b.Quem("admin@umura.dev"), codigo));
    }

    [Fact]
    public async Task LimiteDeIngressosPorPedido()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, lote) = await LotePagoAsync(b);
        var r = await b.Pedidos().CriarAsync(b.Quem("ana@umura.dev"), eventoId, [new Escolha(lote.Id, Pedidos.MaximoPorPedido + 1)]);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task LoteDeOutroEvento_ERecusado()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var (eventoId, _) = await LotePagoAsync(b);
        var (_, outroLote) = await LotePagoAsync(b, "galpao-sessions");
        var r = await b.Pedidos().CriarAsync(b.Quem("ana@umura.dev"), eventoId, [new Escolha(outroLote.Id, 1)]);
        Assert.False(r.Ok);
    }
}
