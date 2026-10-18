using System.Reflection;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Tests.Apoio;
using Agenda_Shows.UI.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agenda_Shows.Tests.Auth;

/// <summary>O papel de quem está com a sessão aberta: quando o cookie reconfere na hora e quando o circuito perde a identidade.</summary>
public class SessaoTests
{
    [Theory]
    [InlineData("GET", "/admin", "navigate", "text/html,application/xhtml+xml", true)]
    [InlineData("GET", "/organizador", null, "text/html", true)]
    [InlineData("POST", "/_blazor/negotiate", "cors", "*/*", true)]
    [InlineData("GET", "/_blazor", "websocket", null, true)]
    [InlineData("GET", "/app.dt2l8tt6a0.css", "no-cors", "text/css,*/*;q=0.1", false)]
    [InlineData("GET", "/img/eventos/shows-1-card.webp", "no-cors", "image/avif,image/webp,*/*", false)]
    [InlineData("GET", "/api/mapa/eventos", "cors", "application/json", false)]
    public void ConferirAgora_SoNaNavegacaoENoCircuito(string metodo, string caminho, string? modo, string? accept, bool esperado)
    {
        var http = new DefaultHttpContext();
        http.Request.Method = metodo;
        http.Request.Path = caminho;
        if (modo is not null) http.Request.Headers["Sec-Fetch-Mode"] = modo;
        if (accept is not null) http.Request.Headers.Accept = accept;
        Assert.Equal(esperado, Sessao.ConferirAgora(http.Request));
    }

    private static async Task<bool> RevalidarAsync(RevalidacaoDoPapel p, AuthenticationState estado)
    {
        var m = typeof(RevalidacaoDoPapel).GetMethod("ValidateAuthenticationStateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<bool>)m.Invoke(p, [estado, CancellationToken.None])!;
    }

    [Fact]
    public async Task Circuito_PerdeAIdentidade_QuandoOPapelMudaNoBanco()
    {
        using var b = await new BancoDeTeste().ComDemoAsync();
        await using var sp = new ServiceCollection().AddScoped<IContas>(_ => b.Contas()).BuildServiceProvider();
        using var revalidacao = new RevalidacaoDoPapel(NullLoggerFactory.Instance, sp.GetRequiredService<IServiceScopeFactory>());
        var cena = new AuthenticationState(b.Quem("cena@umura.dev"));

        Assert.True(await RevalidarAsync(revalidacao, cena));
        Assert.True(await RevalidarAsync(revalidacao, new AuthenticationState(new())), "visitante não tem o que reconferir");

        await using (var db = b.Db())
            await db.Usuarios.Where(u => u.Email == "cena@umura.dev").ExecuteUpdateAsync(s => s.SetProperty(u => u.Papel, Papel.Usuario));
        Assert.False(await RevalidarAsync(revalidacao, cena));
    }
}
