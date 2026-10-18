using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.Options;

namespace Agenda_Shows.UI.Geo;

/// <summary>As opções dos tiles do mapa (seção <c>Mapa:Tiles</c>).</summary>
public sealed class OpcoesDosTiles
{
    public const string Secao = "Mapa:Tiles";

    /// <summary>
    /// De onde vêm os tiles (<c>{z}</c>, <c>{x}</c>, <c>{y}</c>), em PNG: o OpenStreetMap por padrão. A política dele pede que o
    /// endereço dê para trocar sem versão nova — é só configurar.
    /// </summary>
    public string Url { get; set; } = "https://tile.openstreetmap.org/{z}/{x}/{y}.png";

    /// <summary>A pasta do cache em disco; vazia = a temporária do sistema (some no deploy, e o cache se refaz).</summary>
    public string? Pasta { get; set; }

    /// <summary>O endereço público do site: vai no User-Agent e no Referer. Vazio, o Program.cs usa o <c>App:PublicBaseUrl</c>.</summary>
    public string? Site { get; set; }

    /// <summary>
    /// O contato que vai no User-Agent (a política do OSM bloqueia, sem aviso, quem não se identifica). Vazio, o Program.cs
    /// usa o do push (<c>Push:Subject</c>).
    /// </summary>
    public string? Contato { get; set; }
}

/// <summary>
/// Os tiles do mapa pelo PRÓPRIO site (pedido do dono em 15/10/2026, "OpenStreetMap pelo site"): o navegador nunca fala com
/// o servidor de tiles — a CSP segue <c>'self'</c>, e o servidor de tiles não vê quem olha o mapa nem de onde (copiado do MyPods). A régua é a
/// política de uso do OSM (operations.osmfoundation.org/policies/tiles): um User-Agent que identifica o site com um
/// contato, o Referer do site, o cache de <see cref="Validade"/> (o mínimo dela quando o cabeçalho de cache não é lido), o
/// pedido condicional depois disso, no máximo <see cref="MaxBuscasSimultaneas"/> buscas ao mesmo tempo, a pausa depois de
/// uma recusa, e NUNCA busca em lote — só o tile que alguém está olhando.
/// </summary>
/// <remarks>
/// ⚠️ O z/x/y diz que região alguém está olhando (a vizinhança da base): o client <see cref="ClientName"/> não tem logger, e
/// nenhum log daqui leva o tile.
/// </remarks>
public sealed class TilesDoMapa
{
    public const string ClientName = "tiles";

    /// <summary>Quanto um tile vale no cache antes de perguntar de novo ao servidor.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromDays(7);

    /// <summary>O prazo de uma busca inteira (o corpo também): quem espera é um tile na tela.</summary>
    public static readonly TimeSpan PrazoDaBusca = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Depois de uma falha do servidor de tiles (fora do ar, lento, ou recusando o site), quanto tempo o site fica sem
    /// perguntar: serve a cópia velha que tiver, e não martela quem acabou de dizer não.
    /// </summary>
    public static readonly TimeSpan PausaDepoisDeFalha = TimeSpan.FromSeconds(30);

    public const int ZoomMinimo = 3;
    public const int ZoomMaximo = 19;

    /// <summary>Quantas buscas ao servidor de tiles ao mesmo tempo, no processo inteiro.</summary>
    public const int MaxBuscasSimultaneas = 2;

    /// <summary>O teto de um tile: um PNG de mapa tem dezenas de KB; maior que isto não é tile.</summary>
    public const int MaxBytesDoTile = 1_000_000;

    // O Brasil, com folga (Noronha dentro): o tile fora daqui não é mapa de loja nenhuma, e o site não vira proxy do mundo.
    private const double LatSul = -34.5, LatNorte = 6, LngOeste = -74.5, LngLeste = -32;

    private static ReadOnlySpan<byte> AssinaturaPng => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly IHttpClientFactory http;
    private readonly OpcoesDosTiles opcoes;
    private readonly ILogger<TilesDoMapa>? logger;
    private readonly Func<DateTime> agoraUtc;
    private readonly string pasta;
    private readonly SemaphoreSlim buscas = new(MaxBuscasSimultaneas, MaxBuscasSimultaneas);
    private readonly ConcurrentDictionary<string, Lazy<Task<byte[]?>>> emVoo = new(StringComparer.Ordinal);
    private long pausaAte;

    public TilesDoMapa(IHttpClientFactory http, IOptions<OpcoesDosTiles> opcoes, ILogger<TilesDoMapa>? logger = null,
        Func<DateTime>? agoraUtc = null)
    {
        this.http = http;
        this.opcoes = opcoes.Value;
        this.logger = logger;
        this.agoraUtc = agoraUtc ?? (() => DateTime.UtcNow);
        pasta = string.IsNullOrWhiteSpace(this.opcoes.Pasta) ? Path.Combine(Path.GetTempPath(), "umura-tiles") : this.opcoes.Pasta;
    }

    /// <summary>O User-Agent das buscas: o produto, e entre parênteses o site e o contato que houver.</summary>
    public static string UserAgent(string? site, string? contato)
    {
        var partes = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(site)) partes.Add("+" + site.Trim().TrimEnd('/'));
        if (!string.IsNullOrWhiteSpace(contato))
            partes.Add(contato.Trim().Replace("mailto:", "", StringComparison.OrdinalIgnoreCase));
        return partes.Count == 0 ? "UMURA/1.0" : $"UMURA/1.0 ({string.Join("; ", partes)})";
    }

    /// <summary>O tile existe, no zoom que o mapa usa, e cobre um pedaço do Brasil.</summary>
    public static bool Valido(int z, int x, int y)
    {
        if (z < ZoomMinimo || z > ZoomMaximo) return false;
        var n = 1 << z;
        if (x < 0 || y < 0 || x >= n || y >= n) return false;
        var oeste = x / (double)n * 360 - 180;
        var leste = (x + 1) / (double)n * 360 - 180;
        var norte = Latitude(y, n);
        var sul = Latitude(y + 1, n);
        return leste >= LngOeste && oeste <= LngLeste && norte >= LatSul && sul <= LatNorte;
    }

    private static double Latitude(int y, int n) => Math.Atan(Math.Sinh(Math.PI * (1 - 2.0 * y / n))) * 180 / Math.PI;

    /// <summary>
    /// O PNG do tile: do disco, enquanto vale; senão do servidor — uma busca por tile mesmo com muita gente pedindo, com o
    /// pedido condicional quando há cópia velha, que é servida se o servidor não responder. Nulo = não há tile.
    /// </summary>
    public async Task<byte[]?> ObterAsync(int z, int x, int y, CancellationToken ct = default)
    {
        if (!Valido(z, x, y)) return null;
        var arquivo = Path.Combine(pasta, z.ToString(CultureInfo.InvariantCulture), x.ToString(CultureInfo.InvariantCulture),
            y.ToString(CultureInfo.InvariantCulture) + ".png");
        if (Fresco(arquivo)) return await File.ReadAllBytesAsync(arquivo, ct);

        // A busca é do tile, não de quem pediu: quem desiste (fecha a aba) não cancela a dos outros.
        var chave = string.Create(CultureInfo.InvariantCulture, $"{z}/{x}/{y}");
        var voo = emVoo.GetOrAdd(chave, k => new Lazy<Task<byte[]?>>(() => BuscarEDesmarcarAsync(k, z, x, y, arquivo)));
        return await voo.Value.WaitAsync(ct);
    }

    private bool Fresco(string arquivo) => File.Exists(arquivo) && agoraUtc() - File.GetLastWriteTimeUtc(arquivo) < Validade;

    private bool EmPausa() => agoraUtc().Ticks < Interlocked.Read(ref pausaAte);

    private void Pausar() => Interlocked.Exchange(ref pausaAte, (agoraUtc() + PausaDepoisDeFalha).Ticks);

    private static async Task<byte[]?> CopiaVelhaAsync(string arquivo)
        => File.Exists(arquivo) ? await File.ReadAllBytesAsync(arquivo) : null;

    private async Task<byte[]?> BuscarEDesmarcarAsync(string chave, int z, int x, int y, string arquivo)
    {
        try
        {
            return await BuscarAsync(z, x, y, arquivo);
        }
        finally
        {
            emVoo.TryRemove(chave, out _);
        }
    }

    private async Task<byte[]?> BuscarAsync(int z, int x, int y, string arquivo)
    {
        if (EmPausa()) return await CopiaVelhaAsync(arquivo);
        await buscas.WaitAsync();
        try
        {
            // Quem esperou a vez pode encontrar o tile que a busca anterior acabou de gravar — ou a pausa que ela abriu.
            if (Fresco(arquivo)) return await File.ReadAllBytesAsync(arquivo);
            if (EmPausa()) return await CopiaVelhaAsync(arquivo);
            var velho = File.Exists(arquivo);

            using var pedido = new HttpRequestMessage(HttpMethod.Get, Endereco(z, x, y));
            pedido.Headers.TryAddWithoutValidation("User-Agent", UserAgent(opcoes.Site, opcoes.Contato));
            if (Uri.TryCreate(opcoes.Site, UriKind.Absolute, out var site)) pedido.Headers.Referrer = site;
            if (velho) pedido.Headers.IfModifiedSince = File.GetLastWriteTimeUtc(arquivo);

            // Um client NOVO a cada busca (é o contrato do IHttpClientFactory): o prazo e o teto valem para o corpo também,
            // porque a resposta é lida inteira dentro do SendAsync.
            var cliente = http.CreateClient(ClientName);
            cliente.Timeout = PrazoDaBusca;
            cliente.MaxResponseContentBufferSize = MaxBytesDoTile;

            try
            {
                using var resposta = await cliente.SendAsync(pedido);
                if (resposta.StatusCode == HttpStatusCode.NotModified && velho)
                {
                    // Ainda vale: a cópia ganha mais uma validade, sem baixar de novo.
                    Tocar(arquivo);
                    return await File.ReadAllBytesAsync(arquivo);
                }

                var png = resposta.IsSuccessStatusCode ? await resposta.Content.ReadAsByteArrayAsync() : null;
                if (png is not null && EhPng(png))
                {
                    await GravarAsync(arquivo, png);
                    return png;
                }

                // 404 é o servidor dizendo que o tile não existe. O resto é recusa (403/429 = a política do OSM barrou o
                // site), servidor quebrado, ou algo que não é tile — pausa, e o aviso vai sem o z/x/y.
                if (resposta.StatusCode != HttpStatusCode.NotFound)
                {
                    Pausar();
                    logger?.LogWarning("O servidor de tiles respondeu {Status} ({Tipo}); {Saida}.", (int)resposta.StatusCode,
                        resposta.Content.Headers.ContentType?.MediaType ?? "sem tipo", velho ? "servida a cópia velha" : "tile sem imagem");
                }
                return velho ? await File.ReadAllBytesAsync(arquivo) : null;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // Fora do ar, lento demais ou mandando mais que um tile: a cópia velha serve; sem ela, não há tile.
                Pausar();
                logger?.LogWarning("O servidor de tiles não respondeu ({Erro}); {Saida}.", ex.GetType().Name,
                    velho ? "servida a cópia velha" : "tile sem imagem");
                return velho ? await File.ReadAllBytesAsync(arquivo) : null;
            }
        }
        finally
        {
            buscas.Release();
        }
    }

    private static bool EhPng(byte[] png) => png.Length > AssinaturaPng.Length && png.AsSpan().StartsWith(AssinaturaPng);

    private string Endereco(int z, int x, int y) => opcoes.Url
        .Replace("{z}", z.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{x}", x.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{y}", y.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Grava pelo arquivo temporário e move: quem lê no meio nunca pega um PNG pela metade.</summary>
    private async Task GravarAsync(string arquivo, byte[] png)
    {
        var temporario = arquivo + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
            await File.WriteAllBytesAsync(temporario, png);
            File.Move(temporario, arquivo, overwrite: true);
            File.SetLastWriteTimeUtc(arquivo, agoraUtc());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // O disco que recusa só tira o cache: o tile ainda vai para quem pediu.
            logger?.LogWarning("O cache de tiles não gravou ({Erro}).", ex.GetType().Name);
            SemFalhar(() => File.Delete(temporario));
        }
    }

    // Sem o toque, o tile só volta a perguntar ao servidor na próxima vez — a cópia segue válida agora.
    private void Tocar(string arquivo) => SemFalhar(() => File.SetLastWriteTimeUtc(arquivo, agoraUtc()));

    private static void SemFalhar(Action gesto)
    {
        try
        {
            gesto();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
