using System.Threading.RateLimiting;
using Agenda_Shows.Contexts;
using Agenda_Shows.Services;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.UI.Auth;
using Agenda_Shows.UI.Components;
using Agenda_Shows.UI.Data;
using Agenda_Shows.UI.Endpoints;
using Agenda_Shows.UI.Geo;
using Agenda_Shows.UI.Net;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// O prerender manda o estado ao circuito no handshake; o padrão de 32 KB do SignalR derrubava a conexão em
// silêncio no MyPods. A lista do mapa NÃO vai por aí (vai por /api/mapa/eventos), mas o teto fica com folga.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(o => o.MaximumReceiveMessageSize = 128 * 1024);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHealthChecks();

// ── Banco e serviços ────────────────────────────────────────────────────────────────────────────────────────────
// A string de conexão vem dos user-secrets (dev) ou da variável ConnectionStrings__DefaultConnection (produção). Nunca do
// código nem do appsettings — ver docker/mysql/README.md.
builder.Services.AddBancoUmura(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddServicosUmura();
// As chaves do Data Protection no banco: em container sem volume, sem isso cada deploy derrubaria o login de todo mundo.
builder.Services.AddDataProtection().PersistKeysToDbContext<MainContext>().SetApplicationName("UMURA");

// ── Login ───────────────────────────────────────────────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = Sessao.NomeDoCookie;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSeguro.Politica(builder.Environment);
        o.LoginPath = "/entrar";
        o.AccessDeniedPath = "/acesso-negado";
        o.ExpireTimeSpan = TimeSpan.FromDays(7);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = Sessao.ValidarAsync;
        // Em /api o desafio é 401, não o HTML da tela de login (o fetch do PWA não sabe o que fazer com uma página).
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            else ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// O circuito relê o papel no banco a cada minuto (sem isto, aba aberta = papel congelado até fechar).
builder.Services.AddScoped<AuthenticationStateProvider, RevalidacaoDoPapel>();

// ── Rede, proxy e limites ───────────────────────────────────────────────────────────────────────────────────────
var proxies = new TrustedProxyOptions();
builder.Configuration.GetSection("Network").Bind(proxies);
builder.Services.AddSingleton(proxies);
builder.Services.AddSingleton<ClientIpResolver>();

builder.Services.AddAntiforgery(o =>
{
    o.HeaderName = "X-CSRF-TOKEN";
    o.Cookie.Name = "Umura.AntiCsrf";
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSeguro.Politica(builder.Environment);
    o.Cookie.HttpOnly = true;
});

static string ChaveDoCliente(HttpContext http) =>
    http.RequestServices.GetRequiredService<ClientIpResolver>().PartitionKey(http);

static RateLimitPartition<string> Janela(string prefixo, HttpContext http, int porMinuto) =>
    RateLimitPartition.GetFixedWindowLimiter(prefixo + ChaveDoCliente(http),
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = porMinuto, QueueLimit = 0 });

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("padrao", http => Janela("padrao:", http, 120));
    // Entrar e criar conta: 10 por minuto por IP — força bruta de senha não passa disso.
    o.AddPolicy("login", http => Janela("login:", http, 10));
    // Um mapa em tela cheia com pan livre pede centenas de tiles: 300/min (o do MyPods) deixava quadrado cinza.
    o.AddPolicy("mapa", http => Janela("mapa:", http, 1500));
});

// ── Mapa: tiles pelo próprio site (o navegador nunca fala com o servidor de tiles; a CSP segue 'self') ─────────────
builder.Services.AddOptions<OpcoesDosTiles>()
    .BindConfiguration(OpcoesDosTiles.Secao)
    .PostConfigure(o =>
    {
        if (string.IsNullOrWhiteSpace(o.Site)) o.Site = builder.Configuration["App:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(o.Contato)) o.Contato = builder.Configuration["App:Contato"];
    });
builder.Services.AddHttpClient(TilesDoMapa.ClientName).RemoveAllLoggers();
builder.Services.AddSingleton<TilesDoMapa>();

var app = builder.Build();

if (app.Environment.IsProduction() && !app.Services.GetRequiredService<ClientIpResolver>().ConfiaEmProxy)
{
    app.Logger.LogWarning(
        "Network:TrustedProxyHops/KnownNetworks não configurados — o rate limiter vai particionar pelo IP da conexão. " +
        "Atrás de proxy, configure Network__TrustedProxyHops=1 e Network__KnownNetworks__0=<rede do proxy>, senão " +
        "todos os visitantes dividem o mesmo teto (e o mapa sai cinza com 429).");
}

await Boot.PrepararBancoAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

// Reexecuta só GET/HEAD e só fora da /api (o porquê está em StatusCodePageDecisions), e ANTES do UseRouting.
app.UseWhen(
    ctx => StatusCodePageDecisions.DeveReexecutar(ctx.Request.Method, ctx.Request.Path, ctx.Request.Headers.Accept.ToString()),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

var csp = Seguranca.Csp(app.Environment.IsDevelopment());
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        Seguranca.Aplicar(ctx.Response.Headers, csp);
        return Task.CompletedTask;
    });
    await next();
});

// O manifesto do PWA é arquivo estático; o MIME dele não vem no provedor padrão.
var tipos = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
tipos.Mappings[".webmanifest"] = "application/manifest+json";
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = tipos });
app.MapStaticAssets();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapEndpointsUmura();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

/// <summary>Visível para os testes de integração.</summary>
public partial class Program;
