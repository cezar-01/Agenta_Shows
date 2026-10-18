using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Contas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Agenda_Shows.UI.Auth;

/// <summary>
/// O papel dentro do circuito do Blazor. O cookie é reconferido a cada requisição HTTP (<see cref="Sessao.ValidarAsync"/>),
/// mas as telas de admin e de organizador vivem num websocket aberto: sem isto, quem foi rebaixado continuaria com o papel
/// antigo enquanto a aba estivesse aberta. A cada <see cref="RevalidationInterval"/> o papel é relido do banco; mudou (ou a
/// conta sumiu) = o circuito perde a identidade, o <c>AuthorizeRouteView</c> manda para /entrar e o recarregamento da página
/// traz o papel novo (a navegação reconfere na hora — <see cref="Sessao.ConferirAgora"/>).
/// </summary>
/// <remarks>Defesa em profundidade: as ações que mudam dados também conferem o papel no banco (PapelNoBanco, no serviço).</remarks>
public sealed class RevalidacaoDoPapel(ILoggerFactory logs, IServiceScopeFactory escopos) : RevalidatingServerAuthenticationStateProvider(logs)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState estado, CancellationToken ct)
    {
        var user = estado.User;
        if (Autorizacao.UsuarioId(user) is not { } id) return true; // visitante: nada a reconferir
        await using var escopo = escopos.CreateAsyncScope();
        var u = await escopo.ServiceProvider.GetRequiredService<IContas>().ObterAsync(id, ct);
        return u is not null && user.IsInRole(Sessao.NomeDoPapel(u.Papel));
    }
}
