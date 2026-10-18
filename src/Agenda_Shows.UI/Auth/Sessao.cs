using System.Security.Claims;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Contas;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Agenda_Shows.UI.Auth;

/// <summary>
/// A sessão: cookie HttpOnly com a identidade e o papel. O papel é reconferido no banco a cada
/// <see cref="ConferirPapelACada"/> — o admin que rebaixa alguém não espera o cookie vencer em 7 dias.
/// </summary>
public static class Sessao
{
    public const string NomeDoCookie = "Umura.Sessao";
    public const string ClaimConferidoEm = "papel_conferido_em";
    public static readonly TimeSpan ConferirPapelACada = TimeSpan.FromMinutes(2);

    public static string NomeDoPapel(Papel p) => p switch
    {
        Papel.Admin => Papeis.Admin,
        Papel.Organizador => Papeis.Organizador,
        _ => Papeis.Usuario,
    };

    public static ClaimsPrincipal Principal(UsuarioDto u, DateTime agoraUtc)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Nome),
            new(ClaimTypes.Email, u.Email),
            new(ClaimTypes.Role, NomeDoPapel(u.Papel)),
            new(ClaimConferidoEm, agoraUtc.Ticks.ToString()),
        };
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    public static Task EntrarAsync(HttpContext http, UsuarioDto u) =>
        http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, Principal(u, DateTime.UtcNow),
            new AuthenticationProperties { IsPersistent = true });

    /// <summary>
    /// O evento do cookie que reconfere o papel. Conta apagada = sessão derrubada. Abrir página e abrir o circuito do
    /// Blazor reconferem SEMPRE (<see cref="ConferirAgora"/>); o resto usa a conferência de até 2 min.
    /// </summary>
    public static async Task ValidarAsync(CookieValidatePrincipalContext ctx)
    {
        var p = ctx.Principal;
        if (p is null) return;
        var agora = DateTime.UtcNow;
        var fresco = long.TryParse(p.FindFirst(ClaimConferidoEm)?.Value, out var ticks)
                     && agora - new DateTime(ticks, DateTimeKind.Utc) < ConferirPapelACada;
        if (fresco && !ConferirAgora(ctx.HttpContext.Request)) return;

        var id = Autorizacao.UsuarioId(p);
        var contas = ctx.HttpContext.RequestServices.GetRequiredService<IContas>();
        var u = id is null ? null : await contas.ObterAsync(id.Value, ctx.HttpContext.RequestAborted);
        if (u is null)
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        // Conferiu e nada mudou: não reemite o cookie a cada página.
        if (fresco && p.IsInRole(NomeDoPapel(u.Papel)) && p.Identity?.Name == u.Nome) return;
        ctx.ReplacePrincipal(Principal(u, agora));
        ctx.ShouldRenew = true;
    }

    /// <summary>
    /// Navegação (abrir/recarregar uma página) e o circuito do Blazor (/_blazor) reconferem o papel na hora: é com esse
    /// usuário que a página nova e o circuito novo vão trabalhar. Assim o circuito derrubado pela
    /// <see cref="RevalidacaoDoPapel"/> volta já com o papel novo, sem esperar os 2 min do cookie. Scripts, CSS e imagens não
    /// passam por aqui (iriam ao banco a cada arquivo).
    /// </summary>
    public static bool ConferirAgora(HttpRequest r) =>
        r.Path.StartsWithSegments("/_blazor")
        || (HttpMethods.IsGet(r.Method) && (r.Headers["Sec-Fetch-Mode"] == "navigate" || r.Headers.Accept.ToString().Contains("text/html")));

    /// <summary>
    /// O returnUrl só pode ser um caminho deste site: recusa "//", "/\", "\" e caractere de controle (o navegador apaga
    /// TAB e quebra de linha da URL, então "/\t/atacante" virava "//atacante" — open redirect pego no MyPods).
    /// </summary>
    public static string VoltaSegura(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return "/";
        if (returnUrl.Any(char.IsControl)) return "/";
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//") || returnUrl.StartsWith("/\\") || returnUrl.Contains('\\')) return "/";
        return returnUrl;
    }
}
