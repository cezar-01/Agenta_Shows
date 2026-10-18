using Agenda_Shows.Entities;
using Agenda_Shows.Services.Admin;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Services.Demo;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.Services.Organizador;
using Agenda_Shows.Services.Pedidos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agenda_Shows.Services;

public static class Registro
{
    /// <summary>Todos os serviços da UMURA. O banco (<c>AddBancoUmura</c>) é registrado à parte, pelo app.</summary>
    public static IServiceCollection AddServicosUmura(this IServiceCollection s)
    {
        s.TryAddSingleton(TimeProvider.System);
        s.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        s.AddScoped<ICatalogo, Catalogo>();
        s.AddScoped<IFavoritos, Favoritos>();
        s.AddScoped<IContas, Contas.Contas>();
        s.AddScoped<IPedidos, Pedidos.Pedidos>();
        s.AddScoped<IAreaDoOrganizador, AreaDoOrganizador>();
        s.AddScoped<IAreaDoAdmin, AreaDoAdmin>();
        s.AddScoped<Semeadura>();
        s.AddSingleton<IAnalitica, Analitica>();
        s.AddHostedService<ExpiracaoDeReservas>();
        return s;
    }
}

/// <summary>A cada minuto, devolve ao lote os ingressos de reservas que passaram do prazo sem pagar.</summary>
public sealed class ExpiracaoDeReservas(IServiceScopeFactory escopos, ILogger<ExpiracaoDeReservas> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var escopo = escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetRequiredService<IPedidos>().ExpirarVencidosAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Banco fora do ar não derruba o app: a próxima volta tenta de novo.
                log.LogWarning("Expiração de reservas falhou nesta volta ({Erro}).", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
