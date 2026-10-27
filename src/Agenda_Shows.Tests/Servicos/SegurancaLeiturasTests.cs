using Agenda_Shows.Entities;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.Tests.Apoio;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agenda_Shows.Tests.Servicos;

/// <summary>
/// As correções da 2ª verificação (26/10): as LEITURAS do admin e do organizador também conferem o papel no banco (aba aberta
/// de quem foi rebaixado não lê mais nada), o botão das datas da demo passa pelo serviço, e a visualização da mesma pessoa
/// conta uma vez por janela.
/// </summary>
public class SegurancaLeiturasTests
{
    private static async Task MudarPapelNoBancoAsync(BancoDeTeste b, string email, Papel papel)
    {
        await using var db = b.Db();
        await db.Usuarios.Where(u => u.Email == email).ExecuteUpdateAsync(s => s.SetProperty(u => u.Papel, papel));
    }

    [Fact]
    public async Task AdminRebaixado_ComATelaAberta_NaoLeMaisContasPedidosNemPainel()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var adminAntigo = b.Quem("admin@umura.dev");
        string codigoDaAna;
        await using (var db = b.Db())
            codigoDaAna = (await db.Pedidos.FirstAsync(p => p.Usuario!.Email == "ana@umura.dev")).Codigo;
        // Antes de rebaixar, o admin vê tudo — inclusive o pedido de outra pessoa.
        Assert.NotNull(await b.Pedidos().ObterAsync(adminAntigo, codigoDaAna));

        await MudarPapelNoBancoAsync(b, "admin@umura.dev", Papel.Usuario);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Contas().ListarAsync(adminAntigo, null));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().PedidosAsync(adminAntigo, "ana"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().PainelAsync(adminAntigo));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().EventosAsync(adminAntigo, null));
        // O pedido alheio volta a ser "não encontrado".
        Assert.Null(await b.Pedidos().ObterAsync(adminAntigo, codigoDaAna));
    }

    [Fact]
    public async Task AdminRebaixadoAOrganizador_VeSoOsProprioseventos()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var adminAntigo = b.Quem("admin@umura.dev");
        Assert.NotEmpty(await b.Organizador().MeusEventosAsync(adminAntigo));

        await MudarPapelNoBancoAsync(b, "admin@umura.dev", Papel.Organizador);

        // A Curadoria não cadastrou nenhum dos eventos de demonstração: como organizador, a lista dela é vazia.
        Assert.Empty(await b.Organizador().MeusEventosAsync(adminAntigo));
    }

    [Fact]
    public async Task OrganizadorRebaixado_ComATelaAberta_NaoLeMaisOsEventosNemAsVendas()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var cenaAntiga = b.Quem("cena@umura.dev");
        var evento = (await b.Organizador().MeusEventosAsync(cenaAntiga)).First();

        await MudarPapelNoBancoAsync(b, "cena@umura.dev", Papel.Usuario);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().MeusEventosAsync(cenaAntiga));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().PainelAsync(cenaAntiga));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().VendasAsync(cenaAntiga, evento.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Organizador().ParaEditarAsync(cenaAntiga, evento.Id));
    }

    [Fact]
    public async Task DatasDaDemo_SoComAdminNoBanco()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        var adminAntigo = b.Quem("admin@umura.dev");
        await b.Admin().AtualizarDatasDaDemoAsync(adminAntigo); // admin de verdade: passa

        await MudarPapelNoBancoAsync(b, "admin@umura.dev", Papel.Usuario);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().AtualizarDatasDaDemoAsync(adminAntigo));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => b.Admin().AtualizarDatasDaDemoAsync(b.Quem("ana@umura.dev")));
    }

    [Fact]
    public void Visualizacao_DaMesmaPessoa_ContaUmaVezPorJanela()
    {
        // A decisão é testada direto (a gravação é fire-and-forget, num Task.Run): sem banco, sem espera.
        using var sp = new ServiceCollection().BuildServiceProvider();
        var analitica = new Analitica(sp.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<Analitica>.Instance);
        var t0 = BancoDeTeste.Sabado15h;

        Assert.True(analitica.ContaEstaVisita(7, "ip:1.2.3.4", t0));
        Assert.False(analitica.ContaEstaVisita(7, "ip:1.2.3.4", t0.AddMinutes(1)));   // recarregou: não conta
        Assert.False(analitica.ContaEstaVisita(7, "ip:1.2.3.4", t0.AddMinutes(29)));
        Assert.True(analitica.ContaEstaVisita(7, "ip:5.6.7.8", t0.AddMinutes(2)));    // outra pessoa conta
        Assert.True(analitica.ContaEstaVisita(8, "ip:1.2.3.4", t0.AddMinutes(2)));    // outro evento conta
        Assert.True(analitica.ContaEstaVisita(7, "ip:1.2.3.4", t0 + Analitica.JanelaDaVisita)); // a janela passou
        Assert.True(analitica.ContaEstaVisita(7, null, t0));                            // sem visitante: sempre conta
        Assert.True(analitica.ContaEstaVisita(7, null, t0));
    }

    [Fact]
    public void Visualizacao_TrocarONavegadorEmSerie_EsbarraNoTetoDaRede()
    {
        using var sp = new ServiceCollection().BuildServiceProvider();
        var analitica = new Analitica(sp.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System, NullLogger<Analitica>.Instance);
        var t0 = BancoDeTeste.Sabado15h;
        var contadas = Enumerable.Range(0, 100).Count(i => analitica.ContaEstaVisita(7, $"ip:1.2.3.4|ua{i}", t0.AddSeconds(i), "ip:1.2.3.4"));
        Assert.Equal(Analitica.TetoPorRede, contadas);
        Assert.True(analitica.ContaEstaVisita(7, "ip:9.9.9.9|ua", t0.AddMinutes(2), "ip:9.9.9.9"));           // outra rede
        Assert.True(analitica.ContaEstaVisita(8, "ip:1.2.3.4|ua0", t0.AddMinutes(2), "ip:1.2.3.4"));          // outro evento
        Assert.True(analitica.ContaEstaVisita(7, "ip:1.2.3.4|novo", t0 + Analitica.JanelaDaVisita, "ip:1.2.3.4")); // janela nova
    }
}
