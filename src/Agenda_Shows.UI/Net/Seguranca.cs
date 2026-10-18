namespace Agenda_Shows.UI.Net;

/// <summary>
/// A CSP e os cabeçalhos de segurança, numa função pura (o <c>Program.cs</c> é ponto cego de teste — a decisão mora
/// aqui, onde a sentinela alcança). Molde: o MyPods, sem as partes de multi-loja e de prévia.
/// </summary>
public static class Seguranca
{
    /// <summary>
    /// Tudo é da própria origem: os tiles do mapa passam pelo proxy <c>/api/mapa/tiles</c> e a imagem do evento é
    /// servida pelo endpoint próprio — por isso <c>img-src</c> fica em <c>'self' data:</c>. O <c>'wasm-unsafe-eval'</c>
    /// é do Blazor; o <c>'unsafe-inline'</c> do style é dos <c>&lt;style&gt;</c> locais das páginas e dos
    /// <c>style=</c> que o Leaflet escreve. Script inline, nunca.
    /// </summary>
    public static string Csp(bool desenvolvimento)
    {
        var connect = desenvolvimento
            ? "'self' ws://localhost:* wss://localhost:* http://localhost:* https://localhost:*"
            : "'self'";

        return string.Join("; ",
            "default-src 'self'",
            "script-src 'self' 'wasm-unsafe-eval'",
            "style-src 'self' 'unsafe-inline'",
            "font-src 'self'",
            "img-src 'self' data:",
            "manifest-src 'self'",
            "worker-src 'self'",
            "connect-src " + connect,
            "frame-ancestors 'none'",
            "frame-src 'none'",
            "form-action 'self'",
            "object-src 'none'",
            "base-uri 'self'");
    }

    public static void Aplicar(IHeaderDictionary h, string csp)
    {
        h["Content-Security-Policy"] = csp;
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        // O "Perto de mim" precisa do GPS da própria página; câmera e microfone, nunca.
        h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(self)";
        h["X-Frame-Options"] = "DENY";
        h["Cross-Origin-Opener-Policy"] = "same-origin";
        h["Cross-Origin-Resource-Policy"] = "same-origin";
        h.Remove("Server");
        h.Remove("X-Powered-By");
    }
}
