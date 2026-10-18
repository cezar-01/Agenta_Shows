using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Components.Web;

namespace Agenda_Shows.UI.Components;

/// <summary>
/// O &lt;HeadOutlet&gt; segue a página quando ela não é pré-renderizada (molde do MyPods). PageTitle e HeadContent chegam ao
/// head por seção, e seção só liga a um outlet do mesmo renderizador: estático, o outlet só enxerga o SSR — e a página
/// com <c>prerender: false</c> só existe no circuito, então a aba dela ficava sem título. As páginas estáticas e as
/// pré-renderizadas seguem com o head estático (título e SEO no HTML).
/// </summary>
public static class ModoDoHead
{
    public static IComponentRenderMode? Da(HttpContext? http) =>
        Da(http?.GetEndpoint()?.Metadata.GetMetadata<ComponentTypeMetadata>()?.Type);

    public static IComponentRenderMode? Da(Type? pagina) =>
        pagina?.GetCustomAttribute<RenderModeAttribute>(inherit: true)?.Mode is InteractiveServerRenderMode { Prerender: false } modo
            ? modo
            : null;
}
