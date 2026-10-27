using Agenda_Shows.Entities;
using Agenda_Shows.Services.Demo;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Tests.Servicos;

public class SemeaduraECatalogoTests
{
    [Fact]
    public async Task Semeadura_PlantaNichosContasEEventos_EDuasVezesNaoDuplica()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        await b.Semeadura().SemearAsync(BancoDeTeste.SenhaDeTeste, BancoDeTeste.SenhaDeTeste);

        await using var db = b.Db();
        Assert.Equal(DadosDaDemo.Nichos.Length, await db.Categorias.CountAsync());
        Assert.Equal(DadosDaDemo.Eventos.Length, await db.Eventos.CountAsync());
        Assert.Equal(DadosDaDemo.Contas.Length + Semeadura.PessoasDoPublico, await db.Usuarios.CountAsync());
        Assert.True(await db.Pedidos.AnyAsync());

        // Vendidos bate com o que os pedidos pagos seguram: o lote nunca nasce inconsistente.
        var porLote = await db.ItensDoPedido.GroupBy(i => i.TipoDeIngressoId).Select(g => new { g.Key, N = g.Sum(i => i.Quantidade) }).ToListAsync();
        foreach (var l in porLote)
            Assert.Equal(l.N, (await db.TiposDeIngresso.SingleAsync(t => t.Id == l.Key)).Vendidos);
    }

    [Fact]
    public async Task SemSenhaNosSecrets_AsContasNascemSemSenhaConhecida()
    {
        using var b = new BancoDeTeste();
        await b.Semeadura().SemearAsync(null);
        Assert.Null(await b.Contas().AutenticarAsync("admin@umura.dev", BancoDeTeste.SenhaDeTeste));
    }

    [Fact]
    public async Task ComSenha_ContaDeDemoEntra()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var admin = await b.Contas().AutenticarAsync("ADMIN@umura.dev ", BancoDeTeste.SenhaDeTeste);
        Assert.NotNull(admin);
        Assert.Equal(Papel.Admin, admin!.Papel);
    }

    [Fact]
    public async Task Vitrine_TemAoVivoHojeEDestaque_EmQualquerDia()
    {
        // As datas são relativas: semear numa terça às 9h também produz "ao vivo" e "hoje".
        using var b = await new BancoDeTeste(new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc)).ComDemoAsync();
        var v = await b.Catalogo().VitrineAsync();

        Assert.NotEmpty(v.AoVivo);
        Assert.NotEmpty(v.Hoje);
        Assert.NotEmpty(v.Destaques);
        Assert.Equal(DadosDaDemo.Nichos.Length, v.Nichos.Count);
        Assert.All(v.AoVivo, e => Assert.Equal(SituacaoDoEvento.AoVivo, e.SituacaoEm(b.Relogio.Agora)));
    }

    [Fact]
    public async Task AtualizarDatas_TrazAsDatasParaOAgoraDeNovo()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        b.Relogio.Agora = b.Relogio.Agora.AddDays(10);
        Assert.Empty((await b.Catalogo().VitrineAsync()).AoVivo);

        await b.Semeadura().AtualizarDatasAsync();
        Assert.NotEmpty((await b.Catalogo().VitrineAsync()).AoVivo);
    }

    [Fact]
    public async Task Busca_SoGratis_TrazSoEventoSemPreco()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var r = await b.Catalogo().BuscarAsync(new FiltroDeBusca(SoGratis: true, PorPagina: 60));
        Assert.NotEmpty(r.Itens);
        Assert.All(r.Itens, e => Assert.True(e.Gratuito));
    }

    [Fact]
    public async Task Busca_PorTexto_AchaPeloBairro()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var r = await b.Catalogo().BuscarAsync(new FiltroDeBusca(Texto: "Rebouças", PorPagina: 60));
        Assert.NotEmpty(r.Itens);
        Assert.All(r.Itens, e => Assert.True(e.Bairro == "Rebouças" || e.LocalNome.Contains("Rebouças") || e.Titulo.Contains("Rebouças")));
    }

    [Fact]
    public async Task Rascunho_NaoApareceNaVitrineNemNaPagina()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        await using (var db = b.Db())
            await db.Eventos.Where(e => e.Slug == "galpao-sessions").ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, StatusDoEvento.Rascunho));

        Assert.Null(await b.Catalogo().EventoAsync("galpao-sessions", null));
        var busca = await b.Catalogo().BuscarAsync(new FiltroDeBusca(Texto: "Galpão", PorPagina: 60));
        Assert.DoesNotContain(busca.Itens, e => e.Slug == "galpao-sessions");
    }

    [Fact]
    public async Task Nicho_SeparaAoVivoHojeEProximos()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var n = await b.Catalogo().NichoAsync("shows");
        Assert.NotNull(n);
        Assert.Contains(n!.AoVivo, e => e.Slug == "noite-pop-rock-opera-de-arame");
        Assert.All(n.AoVivo.Concat(n.Hoje).Concat(n.Proximos), e => Assert.Equal("shows", e.CategoriaSlug));
        Assert.Null(await b.Catalogo().NichoAsync("nao-existe"));
    }

    [Fact]
    public void Situacao_Rotulos()
    {
        var agora = BancoDeTeste.Sabado15h;
        Assert.Equal("Ao vivo agora", Situacao.Rotulo(agora.AddHours(-1), agora.AddHours(2), StatusDoEvento.Publicado, agora));
        Assert.Equal("Começa em 40 min", Situacao.Rotulo(agora.AddMinutes(40), agora.AddHours(3), StatusDoEvento.Publicado, agora));
        Assert.Equal("Hoje · 20:00", Situacao.Rotulo(agora.AddHours(5), agora.AddHours(8), StatusDoEvento.Publicado, agora));
        Assert.Equal("Encerrado", Situacao.Rotulo(agora.AddHours(-4), agora.AddHours(-1), StatusDoEvento.Publicado, agora));
        Assert.Equal("Cancelado", Situacao.Rotulo(agora.AddHours(1), agora.AddHours(2), StatusDoEvento.Cancelado, agora));
    }
}
