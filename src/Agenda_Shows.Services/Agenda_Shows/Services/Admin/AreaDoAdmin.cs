using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Services.Organizador;
using Agenda_Shows.Services.Pedidos;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Services.Admin;

public sealed record PainelDoAdmin(int Usuarios, int Organizadores, int Admins, int EventosPublicados, int EventosAoVivo,
    int IngressosVendidos, int ReceitaCentavos, int PedidosAguardando, IReadOnlyList<LinhaDoMeuEvento> MaisVendidos,
    IReadOnlyList<(string Nicho, int Vendidos)> VendasPorNicho);

public interface IAreaDoAdmin
{
    Task<PainelDoAdmin> PainelAsync(ClaimsPrincipal quem, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaDoMeuEvento>> EventosAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default);
    Task<Resultado<bool>> AlternarDestaqueAsync(ClaimsPrincipal quem, int eventoId, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoDto>> PedidosAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default);
}

/// <summary>A visão de tudo — e só para o admin: cada método começa pelo <see cref="Autorizacao.EnsureAdmin"/>.</summary>
public sealed class AreaDoAdmin(IDbContextFactory<MainContext> fabrica, TimeProvider relogio) : IAreaDoAdmin
{
    public async Task<PainelDoAdmin> PainelAsync(ClaimsPrincipal quem, CancellationToken ct = default)
    {
        Autorizacao.EnsureAdmin(quem);
        var agora = relogio.GetUtcNow().UtcDateTime;
        await using var db = await fabrica.CreateDbContextAsync(ct);

        var papeis = await db.Usuarios.GroupBy(u => u.Papel).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int Conta(Papel p) => papeis.FirstOrDefault(x => x.Key == p)?.N ?? 0;

        var publicados = await db.Eventos.CountAsync(e => e.Status == StatusDoEvento.Publicado && e.FimUtc > agora, ct);
        var aoVivo = await db.Eventos.CountAsync(e => e.Status == StatusDoEvento.Publicado && e.InicioUtc <= agora && e.FimUtc > agora, ct);
        var pagos = db.Pedidos.Where(p => p.Status == StatusDoPedido.Pago);
        var vendidos = await pagos.SelectMany(p => p.Itens).SumAsync(i => (int?)i.Quantidade, ct) ?? 0;
        var receita = await pagos.SumAsync(p => (int?)p.TotalCentavos, ct) ?? 0;
        var aguardando = await db.Pedidos.CountAsync(p => p.Status == StatusDoPedido.AguardandoPagamento, ct);

        // Ordena ANTES de projetar: o EF não traduz ordenação por membro de um DTO montado no Select.
        var top = await AreaDoOrganizador.Linhas(
                db.Eventos.AsNoTracking().OrderByDescending(e => e.Ingressos.Sum(i => (int?)i.Vendidos) ?? 0).Take(6), db)
            .ToListAsync(ct);
        var porNicho = await db.Pedidos.Where(p => p.Status == StatusDoPedido.Pago)
            .SelectMany(p => p.Itens, (p, i) => new { p.Evento!.Categoria!.Nome, i.Quantidade })
            .GroupBy(x => x.Nome).Select(g => new { Nicho = g.Key, N = g.Sum(x => x.Quantidade) })
            .OrderByDescending(x => x.N).ToListAsync(ct);

        return new PainelDoAdmin(Conta(Papel.Usuario), Conta(Papel.Organizador), Conta(Papel.Admin), publicados, aoVivo, vendidos,
            receita, aguardando, top, porNicho.Select(x => (x.Nicho, x.N)).ToList());
    }

    public async Task<IReadOnlyList<LinhaDoMeuEvento>> EventosAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default)
    {
        Autorizacao.EnsureAdmin(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var q = db.Eventos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var t = busca.Trim();
            q = q.Where(e => e.Titulo.Contains(t) || e.LocalNome.Contains(t) || e.Organizador!.Nome.Contains(t)
                             || (e.Organizador.NomeDoOrganizador != null && e.Organizador.NomeDoOrganizador.Contains(t)));
        }
        return await AreaDoOrganizador.Linhas(q.OrderByDescending(e => e.InicioUtc), db).Take(300).ToListAsync(ct);
    }

    public async Task<Resultado<bool>> AlternarDestaqueAsync(ClaimsPrincipal quem, int eventoId, CancellationToken ct = default)
    {
        Autorizacao.EnsureAdmin(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        await PapelNoBanco.EnsureAdminAsync(db, quem, ct);
        var e = await db.Eventos.FirstOrDefaultAsync(x => x.Id == eventoId, ct);
        if (e is null) return Resultado<bool>.Falha("Evento não encontrado.");
        e.Destaque = !e.Destaque;
        await db.SaveChangesAsync(ct);
        return Resultado<bool>.Sucesso(e.Destaque);
    }

    public async Task<IReadOnlyList<PedidoDto>> PedidosAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default)
    {
        Autorizacao.EnsureAdmin(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var q = db.Pedidos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var t = busca.Trim();
            q = q.Where(p => p.Codigo.Contains(t) || p.Usuario!.Nome.Contains(t) || p.Usuario.Email.Contains(t) || p.Evento!.Titulo.Contains(t));
        }
        return await Pedidos.Pedidos.Projetar(q.OrderByDescending(p => p.CriadoEmUtc)).Take(200).ToListAsync(ct);
    }
}
