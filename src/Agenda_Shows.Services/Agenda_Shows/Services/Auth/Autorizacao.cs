using System.Security.Claims;

namespace Agenda_Shows.Services.Auth;

/// <summary>Os papéis como a claim os carrega — a ÚNICA fonte desses textos no projeto de serviço.</summary>
public static class Papeis
{
    public const string Usuario = "Usuario";
    public const string Organizador = "Organizador";
    public const string Admin = "Admin";

    /// <summary>Para o <c>[Authorize(Roles = ...)]</c> da área do organizador (o admin também entra).</summary>
    public const string OrganizadorOuAdmin = Organizador + "," + Admin;
}

/// <summary>
/// A régua ÚNICA de autorização no serviço (molde do MyPods, onde 58 cópias privadas de <c>EnsureAdmin</c> divergiam
/// justamente no que importa). Defesa em profundidade: a página tem <c>[Authorize]</c>, e o serviço confere de novo.
/// </summary>
/// <remarks>
/// Três regras: (a) autenticação vem ANTES do papel — quem não está logado recebe <see cref="MensagemAutenticacao"/>,
/// nunca "você não é organizador"; (b) a mensagem de domínio é parâmetro, a régua por baixo é a mesma; (c) os
/// predicados <see cref="EhAdmin"/>/<see cref="EhOrganizador"/> não exigem autenticação (substituem o <c>IsInRole</c>
/// inline) — quem quer o portão chama <c>Ensure*</c>.
/// </remarks>
public static class Autorizacao
{
    public const string MensagemAutenticacao = "Entre na sua conta para continuar.";
    public const string MensagemAdmin = "Só a equipe da UMURA pode fazer isso.";
    public const string MensagemOrganizador = "Só organizadores podem fazer isso.";
    public const string MensagemDono = "Só quem cadastrou o evento pode mexer nele.";

    public static bool EstaAutenticado(ClaimsPrincipal? user) => user?.Identity?.IsAuthenticated == true;

    public static bool EhAdmin(ClaimsPrincipal? user) => user?.IsInRole(Papeis.Admin) == true;

    /// <summary>O admin também organiza (a equipe cadastra eventos de curadoria).</summary>
    public static bool EhOrganizador(ClaimsPrincipal? user) =>
        user?.IsInRole(Papeis.Organizador) == true || EhAdmin(user);

    /// <summary>O id da conta na sessão (claim NameIdentifier). Nulo para visitante.</summary>
    public static int? UsuarioId(ClaimsPrincipal? user) =>
        EstaAutenticado(user) && int.TryParse(user!.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id > 0 ? id : null;

    public static int EnsureAuthenticated(ClaimsPrincipal? user) =>
        UsuarioId(user) ?? throw new UnauthorizedAccessException(MensagemAutenticacao);

    public static int EnsureAdmin(ClaimsPrincipal? user, string? mensagem = null)
    {
        var id = EnsureAuthenticated(user);
        if (!EhAdmin(user)) throw new UnauthorizedAccessException(mensagem ?? MensagemAdmin);
        return id;
    }

    public static int EnsureOrganizador(ClaimsPrincipal? user, string? mensagem = null)
    {
        var id = EnsureAuthenticated(user);
        if (!EhOrganizador(user)) throw new UnauthorizedAccessException(mensagem ?? MensagemOrganizador);
        return id;
    }

    /// <summary>O dono do evento, ou um admin. Devolve o id de quem está agindo.</summary>
    public static int EnsureDonoDoEvento(ClaimsPrincipal? user, int organizadorIdDoEvento, string? mensagem = null)
    {
        var id = EnsureOrganizador(user);
        if (EhAdmin(user) || id == organizadorIdDoEvento) return id;
        throw new UnauthorizedAccessException(mensagem ?? MensagemDono);
    }
}
