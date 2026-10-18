using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Services.Auth;

/// <summary>
/// O portão das AÇÕES que mudam dados (mudar papel, salvar/cancelar evento, validar ingresso, destacar): o papel vem do
/// BANCO, não do claim. O claim é uma foto tirada quando o circuito do Blazor abriu — um admin rebaixado com a tela aberta
/// ainda o carrega até a revalidação do circuito (1 min). Aqui o rebaixamento vale no clique seguinte.
/// </summary>
/// <remarks>
/// Sempre depois do <see cref="Autorizacao"/> por claim (que falha rápido sem ir ao banco): este só confirma. Uma consulta
/// por chave primária, no mesmo contexto da ação.
/// </remarks>
internal static class PapelNoBanco
{
    private static async Task<(int Id, Papel Papel)> ConferirAsync(MainContext db, ClaimsPrincipal quem, CancellationToken ct)
    {
        var id = Autorizacao.EnsureAuthenticated(quem);
        var papel = await db.Usuarios.AsNoTracking().Where(u => u.Id == id).Select(u => (Papel?)u.Papel).FirstOrDefaultAsync(ct);
        // Conta apagada com a sessão aberta: trata como quem não entrou.
        return papel is { } p ? (id, p) : throw new UnauthorizedAccessException(Autorizacao.MensagemAutenticacao);
    }

    public static async Task<int> EnsureAdminAsync(MainContext db, ClaimsPrincipal quem, CancellationToken ct)
    {
        Autorizacao.EnsureAdmin(quem);
        var (id, papel) = await ConferirAsync(db, quem, ct);
        return papel == Papel.Admin ? id : throw new UnauthorizedAccessException(Autorizacao.MensagemAdmin);
    }

    public static async Task<int> EnsureOrganizadorAsync(MainContext db, ClaimsPrincipal quem, CancellationToken ct)
    {
        Autorizacao.EnsureOrganizador(quem);
        var (id, papel) = await ConferirAsync(db, quem, ct);
        return papel is Papel.Organizador or Papel.Admin ? id : throw new UnauthorizedAccessException(Autorizacao.MensagemOrganizador);
    }

    /// <summary>O dono do evento (ainda organizador no banco) ou um admin (ainda admin no banco).</summary>
    public static async Task<int> EnsureDonoDoEventoAsync(MainContext db, ClaimsPrincipal quem, int organizadorIdDoEvento, CancellationToken ct)
    {
        Autorizacao.EnsureDonoDoEvento(quem, organizadorIdDoEvento);
        var (id, papel) = await ConferirAsync(db, quem, ct);
        if (papel == Papel.Admin || (papel == Papel.Organizador && id == organizadorIdDoEvento)) return id;
        throw new UnauthorizedAccessException(papel == Papel.Organizador ? Autorizacao.MensagemDono : Autorizacao.MensagemOrganizador);
    }
}
