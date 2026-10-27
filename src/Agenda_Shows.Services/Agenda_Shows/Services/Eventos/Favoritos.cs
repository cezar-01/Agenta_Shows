using System.Text;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agenda_Shows.Services.Eventos;

public interface IFavoritos
{
    /// <summary>Liga/desliga o favorito. Devolve o estado novo.</summary>
    Task<bool> AlternarAsync(ClaimsPrincipal quem, int eventoId, CancellationToken ct = default);
    Task<IReadOnlyList<EventoCard>> MeusAsync(ClaimsPrincipal quem, CancellationToken ct = default);
}

public sealed class Favoritos(IDbContextFactory<MainContext> fabrica, TimeProvider relogio) : IFavoritos
{
    public async Task<bool> AlternarAsync(ClaimsPrincipal quem, int eventoId, CancellationToken ct = default)
    {
        var uid = Autorizacao.EnsureAuthenticated(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var existente = await db.Favoritos.FirstOrDefaultAsync(f => f.UsuarioId == uid && f.EventoId == eventoId, ct);
        if (existente is not null)
        {
            db.Favoritos.Remove(existente);
            await db.SaveChangesAsync(ct);
            return false;
        }
        if (!await db.Eventos.AnyAsync(e => e.Id == eventoId && e.Status != StatusDoEvento.Rascunho, ct)) return false;
        db.Favoritos.Add(new Favorito { UsuarioId = uid, EventoId = eventoId, CriadoEmUtc = relogio.GetUtcNow().UtcDateTime });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { /* clique duplo: a chave composta já decidiu */ }
        return true;
    }

    public async Task<IReadOnlyList<EventoCard>> MeusAsync(ClaimsPrincipal quem, CancellationToken ct = default)
    {
        var uid = Autorizacao.EnsureAuthenticated(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var ids = db.Favoritos.Where(f => f.UsuarioId == uid).Select(f => f.EventoId);
        return await Catalogo.Cards(db.Eventos.AsNoTracking().Where(e => ids.Contains(e.Id) && e.Status != StatusDoEvento.Rascunho)
            .OrderBy(e => e.InicioUtc)).ToListAsync(ct);
    }
}

public interface IAnalitica
{
    /// <summary>
    /// Conta uma visualização da página do evento. Nunca espera, nunca lança: a página não pode pagar por isso.
    /// <paramref name="visitante"/> (a chave do cliente, ex.: o IP do rate limiter) faz a mesma pessoa contar uma vez por
    /// evento a cada <see cref="Analitica.JanelaDaVisita"/> — recarregar em loop não infla o número do organizador.
    /// </summary>
    /// <paramref name="rede"/> (o IP do anônimo) põe um teto por rede no mesmo evento e janela (<see cref="Analitica.TetoPorRede"/>):
    /// trocar o User-Agent em série não infla o número.
    void RegistrarVisualizacao(int eventoId, string? visitante = null, string? rede = null);
}

/// <summary>
/// Fire-and-forget com escopo próprio (molde do ProductAnalytics do MyPods): o registro roda depois da página e sobrevive
/// a ela. Singleton — por isso pede um escopo novo a cada gravação, nunca segura um contexto.
/// </summary>
public sealed class Analitica(IServiceScopeFactory escopos, TimeProvider relogio, ILogger<Analitica> log) : IAnalitica
{
    /// <summary>A mesma pessoa no mesmo evento dentro desta janela é uma visita só.</summary>
    public static readonly TimeSpan JanelaDaVisita = TimeSpan.FromMinutes(30);

    // (evento|visitante) → quando contou. Só na memória (nada de IP no banco). Limpa os vencidos quando cresce — no máximo
    // uma vez por minuto (varrer a cada visita custaria caro com muita gente) — e, se nem assim couber, zera: o pior caso é
    // contar de novo quem voltou, nunca crescer sem fim.
    private readonly ConcurrentDictionary<string, DateTime> vistos = new(StringComparer.Ordinal);
    private const int LimparAcimaDe = 20_000;
    private const int ZerarAcimaDe = 200_000;
    private long ultimaLimpeza;

    /// <summary>Quantas visitas DIFERENTES uma mesma rede (IP) conta no mesmo evento dentro da janela — a turma no Wi-Fi da faculdade cabe.</summary>
    public const int TetoPorRede = 20;

    // (evento|rede) → início da janela e quantas visitas já contaram nela. Mesma regra de memória do "vistos".
    private readonly ConcurrentDictionary<string, (DateTime Desde, int Contadas)> porRede = new(StringComparer.Ordinal);

    public void RegistrarVisualizacao(int eventoId, string? visitante = null, string? rede = null)
    {
        var quando = relogio.GetUtcNow().UtcDateTime;
        if (!ContaEstaVisita(eventoId, visitante, quando, rede)) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                var fabrica = escopo.ServiceProvider.GetRequiredService<IDbContextFactory<MainContext>>();
                await using var db = await fabrica.CreateDbContextAsync();
                db.Visualizacoes.Add(new VisualizacaoDeEvento { EventoId = eventoId, QuandoUtc = quando });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                log.LogWarning("Visualização não registrada ({Erro}).", ex.GetType().Name);
            }
        });
    }

    /// <summary>
    /// A decisão (síncrona, sem banco): esta visita conta? Sem visitante conhecido, sempre conta; com, só a primeira da
    /// pessoa no evento dentro da <see cref="JanelaDaVisita"/>.
    /// </summary>
    public bool ContaEstaVisita(int eventoId, string? visitante, DateTime agora, string? rede = null)
    {
        if (string.IsNullOrEmpty(visitante)) return true;
        var chaveDaRede = string.IsNullOrEmpty(rede) ? null : $"{eventoId}|{Embaralhar(rede)}";
        // A rede já contou o teto nesta janela: ninguém novo dela conta (nem marca o visitante, que pode contar depois).
        if (chaveDaRede is not null && porRede.TryGetValue(chaveDaRede, out var r) && agora - r.Desde < JanelaDaVisita && r.Contadas >= TetoPorRede)
            return false;
        if (!PrimeiraDaJanela($"{eventoId}|{Embaralhar(visitante)}", agora)) return false;
        if (chaveDaRede is not null)
            porRede.AddOrUpdate(chaveDaRede, _ => (agora, 1),
                (_, antes) => agora - antes.Desde >= JanelaDaVisita ? (agora, 1) : (antes.Desde, antes.Contadas + 1));
        return true;
    }

    // A chave guardada é um HMAC do visitante com um sal sorteado no boot (some quando o servidor reinicia): nem o IP nem a
    // conta ficam em claro na memória, e o mesmo visitante dá a mesma marca só dentro deste processo.
    private readonly byte[] sal = RandomNumberGenerator.GetBytes(32);

    private string Embaralhar(string visitante) =>
        Convert.ToHexString(HMACSHA256.HashData(sal, Encoding.UTF8.GetBytes(visitante)), 0, 12);

    private bool PrimeiraDaJanela(string chave, DateTime agora)
    {
        if (vistos.Count > LimparAcimaDe)
        {
            var antes = Interlocked.Read(ref ultimaLimpeza);
            if (agora.Ticks - antes >= TimeSpan.TicksPerMinute && Interlocked.CompareExchange(ref ultimaLimpeza, agora.Ticks, antes) == antes)
            {
                // Remove só se o valor ainda é o vencido que foi lido (a mesma visita pode ter acabado de ser renovada).
                foreach (var (k, v) in vistos)
                    if (agora - v >= JanelaDaVisita) vistos.TryRemove(KeyValuePair.Create(k, v));
                foreach (var (k, v) in porRede)
                    if (agora - v.Desde >= JanelaDaVisita) porRede.TryRemove(KeyValuePair.Create(k, v));
                if (vistos.Count > ZerarAcimaDe) vistos.Clear();
                if (porRede.Count > ZerarAcimaDe) porRede.Clear();
            }
        }
        // O AddOrUpdate pode rodar os delegates mais de uma vez (corrida com outro pedido igual): o que vale é o ÚLTIMO, então
        // cada ramo diz o seu resultado — senão um "contou" da tentativa que perdeu vazava e a visita contava duas vezes.
        var contou = false;
        vistos.AddOrUpdate(chave,
            _ => { contou = true; return agora; },
            (_, antes) => { contou = agora - antes >= JanelaDaVisita; return contou ? agora : antes; });
        return contou;
    }
}
