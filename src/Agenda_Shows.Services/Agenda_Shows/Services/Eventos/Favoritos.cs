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
    /// <summary>Conta uma visualização da página do evento. Nunca espera, nunca lança: a página não pode pagar por isso.</summary>
    void RegistrarVisualizacao(int eventoId);
}

/// <summary>
/// Fire-and-forget com escopo próprio (molde do ProductAnalytics do MyPods): o registro roda depois da página e sobrevive
/// a ela. Singleton — por isso pede um escopo novo a cada gravação, nunca segura um contexto.
/// </summary>
public sealed class Analitica(IServiceScopeFactory escopos, TimeProvider relogio, ILogger<Analitica> log) : IAnalitica
{
    public void RegistrarVisualizacao(int eventoId)
    {
        var quando = relogio.GetUtcNow().UtcDateTime;
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
}
