using Agenda_Shows.Services.Comum;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.UI.Geo;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Agenda_Shows.UI.Endpoints;

public static class Endpoints
{
    public static WebApplication MapEndpointsUmura(this WebApplication app)
    {
        // Sair é POST com antiforgery (link de GET para sair deixaria qualquer site deslogar a pessoa).
        app.MapPost("/sair", async (HttpContext http) =>
        {
            // Sem sessão (já saiu em outra aba, conta apagada): sair não muda nada, então não precisa de token — vai para a home.
            if (http.User.Identity?.IsAuthenticated != true) return Results.LocalRedirect("/");
            // O UseAntiforgery só CONFERE e anota o resultado na feature; quem recusa é o binding de formulário — e este
            // handler não lê formulário. Então a recusa é aqui: sem feature (middleware não rodou) ou token inválido = não sai
            // (pode ser outro site tentando deslogar a pessoa). Mas sem tela 400 em branco: vai para Minha conta, onde o botão
            // Sair já vem com token novo.
            if (http.Features.Get<IAntiforgeryValidationFeature>() is not { IsValid: true }) return Results.LocalRedirect("/minha-conta?sair=refazer");
            await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        })
        // A metadata é o que faz o UseAntiforgery conferir este endpoint.
        .WithMetadata(new RequireAntiforgeryTokenAttribute());

        // Os eventos do mapa, em JSON (lidos pelo mapa-eventos.js). A lista NÃO vai pelo estado do prerender — 32 KB derrubam
        // o circuito em silêncio (gotcha do MyPods).
        app.MapGet("/api/mapa/eventos", async (string? nicho, ICatalogo catalogo, TimeProvider relogio, CancellationToken ct) =>
        {
            var agora = relogio.GetUtcNow().UtcDateTime;
            var eventos = await catalogo.MapaAsync(nicho, ct);
            return Results.Ok(eventos.Select(e => new
            {
                e.Slug, e.Titulo, nicho = e.CategoriaSlug, nichoNome = e.CategoriaNome, e.LocalNome, e.Bairro,
                lat = e.Latitude, lng = e.Longitude, quando = e.RotuloEm(agora),
                aoVivo = e.SituacaoEm(agora) == SituacaoDoEvento.AoVivo, preco = e.Preco, foto = Foto.Card(e.ImagemUrl),
            }));
        }).RequireRateLimiting("padrao");

        // Os tiles do mapa pelo próprio site (proxy com cache do MyPods). PÚBLICO: o mapa é a vitrine do visitante.
        app.MapGet("/api/mapa/tiles/{z:int}/{x:int}/{y:int}.png", async (int z, int x, int y, HttpContext http, TilesDoMapa tiles,
            LimiteDeBuscasDeTiles limite, ILogger<TilesDoMapa> log) =>
        {
            try
            {
                // Fora de Curitiba e região (ou impossível): nem cobra vaga.
                if (!TilesDoMapa.Valido(z, x, y)) return Results.NotFound();
                // Tile que ainda não está no cache vai ao OSM: esse tem limite próprio por IP e do site (raspagem de tiles nunca
                // vistos esbarra aqui; quem só anda pelo mapa já visto não). Sem vaga, a cópia velha (se houver) em vez do cinza.
                if (!tiles.EmCache(z, x, y) && !limite.Permitir(http))
                {
                    if (await tiles.CopiaGuardadaAsync(z, x, y) is { } velha)
                    {
                        http.Response.Headers.CacheControl = "private, max-age=60";
                        return Results.File(velha, "image/png");
                    }
                    http.Response.Headers.RetryAfter = "60";
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }
                var png = await tiles.ObterAsync(z, x, y, http.RequestAborted);
                if (png is null) return Results.NotFound();
                // PRIVADO: a URL diz que região alguém olhou, e nenhum cache no caminho guarda isso para outro.
                http.Response.Headers.CacheControl = "private, max-age=" + (int)TilesDoMapa.Validade.TotalSeconds;
                return Results.File(png, "image/png");
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                return Results.Empty;
            }
            catch (Exception ex)
            {
                log.LogError("Falha ao servir um tile do mapa ({Erro}).", ex.GetType().Name);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireRateLimiting("mapa");

        return app;
    }
}
