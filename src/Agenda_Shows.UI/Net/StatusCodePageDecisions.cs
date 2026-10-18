namespace Agenda_Shows.UI.Net;

/// <summary>
/// Quando a página de status (/not-found) reexecuta (copiado do MyPods). A reexecução mantém o MÉTODO, e a página
/// Razor só serve GET/HEAD: num POST ela falharia e o status dela apagaria o original (um 403 virava 400). E a
/// <c>/api</c> devolve o status cru, a não ser que quem pediu seja um navegador querendo HTML.
/// </summary>
public static class StatusCodePageDecisions
{
    public static bool DeveReexecutar(string metodo, PathString caminho, string? accept)
    {
        if (!HttpMethods.IsGet(metodo) && !HttpMethods.IsHead(metodo))
            return false;

        return !caminho.StartsWithSegments("/api") || AceitaHtml(accept);
    }

    private static bool AceitaHtml(string? accept)
        => accept is not null && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase);
}
