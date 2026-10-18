namespace Agenda_Shows.UI.Net;

/// <summary>
/// A régua do <c>Secure</c> dos cookies (copiada do MyPods): por AMBIENTE, nunca por <c>Request.IsHttps</c> — atrás do
/// proxy o Kestrel vê HTTP e o cookie nasceria sem Secure. Em Development vale o que a requisição for (o localhost
/// em http segue funcionando).
/// </summary>
public static class CookieSeguro
{
    public static bool Secure(HttpContext http)
    {
        var env = http.RequestServices?.GetService<IHostEnvironment>();
        return env is null
            ? http.Request.IsHttps
            : !env.IsDevelopment() || http.Request.IsHttps;
    }

    public static CookieSecurePolicy Politica(IHostEnvironment env)
        => env.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
}
