using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace Agenda_Shows.Contexts;

/// <summary>Como a UMURA fala com o MySQL — num lugar só, para o app e para o <c>dotnet ef</c> dizerem a mesma coisa.</summary>
public static class Banco
{
    /// <summary>
    /// A versão fixa do servidor: com ela o Pomelo não precisa abrir conexão para descobrir a versão (o
    /// <c>dotnet ef migrations add</c> funciona com o banco desligado) e o boot não depende de um round-trip a mais.
    /// </summary>
    public static readonly MySqlServerVersion Versao = new(new Version(8, 0, 36));

    /// <summary>
    /// A string de conexão NUNCA mora no código nem no appsettings: em dev ela vem dos user-secrets do projeto UI
    /// (<c>dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..." --project src/Agenda_Shows.UI</c>);
    /// em produção, da variável de ambiente <c>ConnectionStrings__DefaultConnection</c>.
    /// </summary>
    public static IServiceCollection AddBancoUmura(this IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Falta ConnectionStrings:DefaultConnection. Em dev ela fica nos user-secrets do projeto UI — " +
                "veja docker/mysql/README.md (passo 'Conectar o app').");

        services.AddDbContextFactory<MainContext>(o => Configurar(o, connectionString), ServiceLifetime.Scoped);
        return services;
    }

    /// <summary>
    /// "Data Source=arquivo.db" = pré-visualização com SQLite (sem Docker, só para olhar as telas; o boot cria as tabelas
    /// direto, sem migration). Qualquer outra string = MySQL, o banco de verdade.
    /// </summary>
    public static bool EhPreviaSqlite(string? connectionString) =>
        (connectionString ?? string.Empty).TrimStart().StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase);

    public static void Configurar(DbContextOptionsBuilder o, string connectionString)
    {
        if (EhPreviaSqlite(connectionString))
        {
            o.UseSqlite(connectionString);
            return;
        }

        // Pool com piso pequeno e vida curta: soquetes quentes sem segurar conexão velha atrás de proxy.
        var cs = new MySqlConnectionStringBuilder(connectionString);
        if (cs.MinimumPoolSize == 0) cs.MinimumPoolSize = 2;
        if (cs.ConnectionLifeTime == 0) cs.ConnectionLifeTime = 300;

        o.UseMySql(cs.ConnectionString, Versao, m =>
        {
            m.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            m.CommandTimeout(30);
        });
    }
}

/// <summary>
/// O <c>dotnet ef migrations add</c> cria o contexto por aqui. Gerar migration não abre conexão (a versão do servidor é
/// fixa), então sem a variável <c>UMURA_DB</c> ele usa uma string SEM credencial nenhuma — que serve para gerar o
/// código e falha de propósito se alguém tentar <c>database update</c> com ela. Quem aplica a migration em dev é o
/// próprio app no boot, com a string dos user-secrets.
/// </summary>
public sealed class MainContextFactory : IDesignTimeDbContextFactory<MainContext>
{
    public MainContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("UMURA_DB") ?? "server=localhost;database=umura";
        var o = new DbContextOptionsBuilder<MainContext>();
        Banco.Configurar(o, cs);
        return new MainContext(o.Options);
    }
}
