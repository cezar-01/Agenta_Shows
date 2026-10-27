using System.Threading.RateLimiting;
using Agenda_Shows.UI.Net;
using Microsoft.Extensions.Options;

namespace Agenda_Shows.UI.Geo;

/// <summary>
/// O limite das buscas NOVAS de tiles por IP (as que não estão no cache e por isso vão ao servidor de tiles). A política
/// "mapa" do rate limiter conta todo tile, inclusive os do cache, e precisa ser larga (um mapa em tela cheia pede centenas);
/// esta só conta o que custa — é ela que segura quem tenta baixar Curitiba inteira no zoom 19.
/// </summary>
public sealed class LimiteDeBuscasDeTiles : IDisposable
{
    private readonly ClientIpResolver ip;
    private readonly PartitionedRateLimiter<string> limitador;

    // O orçamento do site inteiro com o servidor de tiles (muitos IPs juntos não viram download em massa do OSM): rajada
    // de 300 — a turma abrindo o mapa ao mesmo tempo — e 3 por segundo depois disso (~260 mil por dia, no máximo).
    private readonly TokenBucketRateLimiter global = new(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 300, TokensPerPeriod = 3, ReplenishmentPeriod = TimeSpan.FromSeconds(1), AutoReplenishment = true, QueueLimit = 0,
    });

    public LimiteDeBuscasDeTiles(ClientIpResolver ip, IOptions<OpcoesDosTiles> opcoes)
    {
        this.ip = ip;
        var porMinuto = Math.Max(1, opcoes.Value.BuscasNovasPorMinutoPorIp);
        limitador = PartitionedRateLimiter.Create<string, string>(chave => RateLimitPartition.GetFixedWindowLimiter(chave,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = porMinuto, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    }

    /// <summary>Este pedido ainda cabe no minuto do IP e no orçamento do site? (consome uma vaga de cada se couber)</summary>
    public bool Permitir(HttpContext http)
    {
        using var vaga = limitador.AttemptAcquire(ip.PartitionKey(http));
        if (!vaga.IsAcquired) return false;
        using var doSite = global.AttemptAcquire();
        return doSite.IsAcquired;
    }

    public void Dispose()
    {
        limitador.Dispose();
        global.Dispose();
    }
}
