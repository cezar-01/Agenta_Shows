using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Contas;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;

namespace Agenda_Shows.UI.Auth;

/// <summary>
/// O papel dentro do circuito do Blazor. O cookie é reconferido a cada requisição HTTP (<see cref="Sessao.ValidarAsync"/>),
/// mas as telas de admin e de organizador vivem num websocket aberto: sem isto, quem foi rebaixado continuaria com o papel
/// antigo enquanto a aba estivesse aberta. A cada <see cref="RevalidationInterval"/> o papel é relido do banco; mudou (ou a
/// conta sumiu) = o circuito perde a identidade e a <c>SessaoViva</c> da página recarrega tudo — a navegação reconfere o
/// papel na hora (<see cref="Sessao.ConferirAgora"/>) e o [Authorize] da página decide de novo.
/// </summary>
/// <remarks>Defesa em profundidade: as ações e as leituras restritas também conferem o papel no banco (PapelNoBanco).</remarks>
public sealed class RevalidacaoDoPapel(ILoggerFactory logs, IServiceScopeFactory escopos) : RevalidatingServerAuthenticationStateProvider(logs)
{
    private readonly ILogger log = logs.CreateLogger<RevalidacaoDoPapel>();

    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(AuthenticationState estado, CancellationToken ct)
    {
        var user = estado.User;
        if (Autorizacao.UsuarioId(user) is not { } id) return true; // visitante: nada a reconferir
        try
        {
            await using var escopo = escopos.CreateAsyncScope();
            var u = await escopo.ServiceProvider.GetRequiredService<IContas>().ObterAsync(id, ct);
            return u is not null && user.IsInRole(Sessao.NomeDoPapel(u.Papel));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // O laço foi cancelado (reconexão trocou o usuário do circuito): o MySqlConnector lança OperationCanceledException,
            // que a base trataria como "inválido" e deslogaria por cima do usuário recém-posto. Sair limpo.
            return true;
        }
        catch (Exception ex)
        {
            // Banco fora por um instante não derruba a sessão de ninguém (nem apaga o formulário que a pessoa está
            // preenchendo): mantém e tenta de novo no próximo minuto. O portão de verdade é o PapelNoBanco, nas ações.
            log.LogWarning("Revalidação do papel adiada ({Erro}).", ex.GetType().Name);
            return true;
        }
    }
}
