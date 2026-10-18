using Agenda_Shows.Entities;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Services.Organizador;
using Agenda_Shows.Services.Pedidos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Tests.Servicos;

/// <summary>As três contas: o organizador só enxerga o que é dele, o admin enxerga tudo, o usuário comum não entra em nada.</summary>
public class PapeisTests
{
    [Fact]
    public async Task Organizador_SoVeOsProprios_AdminVeTodos()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = await b.Organizador().MeusEventosAsync(b.Quem("cena@umura.dev"));
        var palco = await b.Organizador().MeusEventosAsync(b.Quem("palco@umura.dev"));
        var admin = await b.Organizador().MeusEventosAsync(b.Quem("admin@umura.dev"));

        Assert.NotEmpty(cena);
        Assert.All(cena, e => Assert.Equal("Coletivo Cena Livre", e.Organizador));
        Assert.Empty(cena.Select(e => e.Id).Intersect(palco.Select(e => e.Id)));
        Assert.Equal(await b.Db().Eventos.CountAsync(), admin.Count);
    }

    [Fact]
    public async Task Organizador_NaoAbreNemEditaNemCancelaEventoDeOutro()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var doPalco = (await b.Organizador().MeusEventosAsync(b.Quem("palco@umura.dev"))).First();
        var cena = b.Quem("cena@umura.dev");

        Assert.Null(await b.Organizador().ParaEditarAsync(cena, doPalco.Id));
        Assert.Null(await b.Organizador().VendasAsync(cena, doPalco.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().CancelarAsync(cena, doPalco.Id));

        var form = await b.Organizador().ParaEditarAsync(b.Quem("admin@umura.dev"), doPalco.Id);
        form!.Titulo = "Tomado";
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().SalvarAsync(cena, form));
    }

    [Fact]
    public async Task UsuarioComum_NaoEntraNaAreaDoOrganizadorNemDoAdmin()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var ana = b.Quem("ana@umura.dev");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().PainelAsync(ana));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().PainelAsync(ana));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Contas().ListarAsync(ana, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().PainelAsync(b.Quem("cena@umura.dev")));
    }

    [Fact]
    public async Task Organizador_CriaEvento_ESoEleOVe()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = b.Quem("cena@umura.dev");
        var nicho = await b.Db().Categorias.SingleAsync(c => c.Slug == "shows");
        var form = new EventoForm
        {
            Titulo = "Show Teste do Coletivo", Resumo = "Um show de teste", Descricao = "Descrição", CategoriaId = nicho.Id,
            LocalNome = "Bar Teste", Endereco = "Rua Teste, 1", Bairro = "Centro", Latitude = -25.43, Longitude = -49.27,
            InicioLocal = new DateTime(2026, 10, 24, 20, 0, 0), FimLocal = new DateTime(2026, 10, 24, 23, 0, 0),
            ImagemUrl = "/img/eventos/shows-2", Lotes = [new LoteForm { Nome = "Pista", Preco = 30m, Quantidade = 50 }],
        };

        var r = await b.Organizador().SalvarAsync(cena, form);

        Assert.True(r.Ok, r.Erro);
        Assert.Equal("show-teste-do-coletivo", r.Valor);
        Assert.Contains(await b.Organizador().MeusEventosAsync(cena), e => e.Slug == r.Valor);
        Assert.DoesNotContain(await b.Organizador().MeusEventosAsync(b.Quem("palco@umura.dev")), e => e.Slug == r.Valor);
        var evento = await b.Catalogo().EventoAsync(r.Valor!, null);
        Assert.Equal(3000, evento!.Lotes.Single().PrecoCentavos);
    }

    [Fact]
    public async Task Organizador_NaoTiraLoteQueJaVendeu()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = b.Quem("cena@umura.dev");
        var comVenda = (await b.Organizador().MeusEventosAsync(cena)).First(e => e.Vendidos > 0);
        var form = await b.Organizador().ParaEditarAsync(cena, comVenda.Id);
        var vendido = await b.Db().TiposDeIngresso.FirstAsync(t => t.EventoId == comVenda.Id && t.Vendidos > 0);
        form!.Lotes.RemoveAll(l => l.Id == vendido.Id);
        if (form.Lotes.Count == 0) form.Lotes.Add(new LoteForm { Nome = "Novo", Preco = 0, Quantidade = 10 });

        var r = await b.Organizador().SalvarAsync(cena, form);
        Assert.False(r.Ok);
    }

    [Fact]
    public async Task CheckIn_SoUmaVez_ESoDoProprioEvento()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = b.Quem("cena@umura.dev");
        var evento = (await b.Organizador().MeusEventosAsync(cena)).First(e => e.Vendidos > 0);
        await using var db = b.Db();
        var ingresso = await db.Ingressos.FirstAsync(i => i.EventoId == evento.Id);

        Assert.True((await b.Organizador().ValidarIngressoAsync(cena, evento.Id, ingresso.Codigo.ToLowerInvariant())).Ok);
        Assert.False((await b.Organizador().ValidarIngressoAsync(cena, evento.Id, ingresso.Codigo)).Ok);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            b.Organizador().ValidarIngressoAsync(b.Quem("palco@umura.dev"), evento.Id, ingresso.Codigo));
    }

    [Fact]
    public async Task OrganizadorVeONomeDoComprador_NuncaOEmail()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cena = b.Quem("cena@umura.dev");
        var evento = (await b.Organizador().MeusEventosAsync(cena)).First(e => e.Vendidos > 0);
        var vendas = await b.Organizador().VendasAsync(cena, evento.Id);
        Assert.All(vendas!.Pedidos, p => Assert.DoesNotContain("@", p.Comprador));
    }

    [Fact]
    public async Task Cadastro_EmailEmMinusculas_DuplicadoRecusado_AdminNuncaPeloCadastro()
    {
        using var b = new BancoDeTeste();
        var r = await b.Contas().CadastrarAsync(new Cadastro("Zé Teste", " Ze@Exemplo.com ", "senha1234", false, null));
        Assert.True(r.Ok, r.Erro);
        Assert.Equal("ze@exemplo.com", r.Valor!.Email);
        Assert.Equal(Papel.Usuario, r.Valor.Papel);

        Assert.False((await b.Contas().CadastrarAsync(new Cadastro("Outro", "ZE@exemplo.com", "senha1234", false, null))).Ok);
        Assert.False((await b.Contas().CadastrarAsync(new Cadastro("Fraca", "f@exemplo.com", "12345678", false, null))).Ok);

        var org = await b.Contas().CadastrarAsync(new Cadastro("Org", "org@exemplo.com", "senha1234", true, "Coletivo X"));
        Assert.Equal(Papel.Organizador, org.Valor!.Papel);
        Assert.Null(await b.Contas().AutenticarAsync("ze@exemplo.com", "errada123"));
    }

    [Fact]
    public async Task Admin_MudaPapel_MasNaoTiraOProprioAdmin()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var admin = b.Quem("admin@umura.dev");
        var ana = await b.Db().Usuarios.SingleAsync(u => u.Email == "ana@umura.dev");
        var eu = await b.Db().Usuarios.SingleAsync(u => u.Email == "admin@umura.dev");

        Assert.Equal(Papel.Organizador, (await b.Contas().MudarPapelAsync(admin, ana.Id, Papel.Organizador)).Valor!.Papel);
        Assert.False((await b.Contas().MudarPapelAsync(admin, eu.Id, Papel.Usuario)).Ok);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Contas().MudarPapelAsync(b.Quem("cena@umura.dev"), ana.Id, Papel.Admin));
    }

    [Fact]
    public async Task PainelDoAdmin_SomaTudo()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var p = await b.Admin().PainelAsync(b.Quem("admin@umura.dev"));
        Assert.True(p.EventosAoVivo > 0);
        Assert.True(p.IngressosVendidos > 0);
        Assert.Equal(3, p.Organizadores);
        Assert.NotEmpty(p.VendasPorNicho);
    }

    [Fact]
    public async Task Favorito_LigaEDesliga()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var ana = b.Quem("ana@umura.dev");
        var id = (await b.Db().Eventos.FirstAsync()).Id;
        Assert.True(await b.Favoritos().AlternarAsync(ana, id));
        Assert.Single(await b.Favoritos().MeusAsync(ana));
        Assert.False(await b.Favoritos().AlternarAsync(ana, id));
        Assert.Empty(await b.Favoritos().MeusAsync(ana));
    }
}
