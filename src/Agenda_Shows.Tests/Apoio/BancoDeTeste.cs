using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Admin;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Services.Demo;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.Services.Organizador;
using Agenda_Shows.Services.Pedidos;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agenda_Shows.Tests.Apoio;

/// <summary>O relógio dos testes: parado no instante que o teste escolhe, e anda só quando o teste manda.</summary>
public sealed class RelogioFixo(DateTime utc) : TimeProvider
{
    public DateTime Agora { get; set; } = utc;
    public override DateTimeOffset GetUtcNow() => new(Agora, TimeSpan.Zero);
}

/// <summary>
/// Um banco SQLite em memória com o MESMO modelo do MySQL (o MainContext de verdade) e os serviços de verdade por cima.
/// Nenhum teste precisa do MySQL. A conexão fica aberta enquanto o banco vive: fechar apagaria o banco em memória.
/// </summary>
public sealed class BancoDeTeste : IDisposable
{
    // Sábado, 17/10/2026, 15:00 em Brasília.
    public static readonly DateTime Sabado15h = new(2026, 10, 17, 18, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection conexao;
    public RelogioFixo Relogio { get; }
    public IDbContextFactory<MainContext> Fabrica { get; }
    public PasswordHasher<Usuario> Hasher { get; } = new();

    public BancoDeTeste(DateTime? agora = null)
    {
        Relogio = new RelogioFixo(agora ?? Sabado15h);
        conexao = new SqliteConnection("DataSource=:memory:");
        conexao.Open();
        var opcoes = new DbContextOptionsBuilder<MainContext>().UseSqlite(conexao).Options;
        Fabrica = new FabricaSqlite(opcoes);
        using var db = Fabrica.CreateDbContext();
        db.Database.EnsureCreated();
    }

    public MainContext Db() => Fabrica.CreateDbContext();

    public Semeadura Semeadura() => new(Fabrica, Hasher, Relogio, NullLogger<Semeadura>.Instance);
    public Catalogo Catalogo() => new(Fabrica, Relogio);
    public Pedidos Pedidos() => new(Fabrica, Relogio, NullLogger<Pedidos>.Instance);
    public Contas Contas() => new(Fabrica, Hasher, Relogio);
    public AreaDoOrganizador Organizador() => new(Fabrica, Relogio);
    public AreaDoAdmin Admin() => new(Fabrica, Relogio);
    public Favoritos Favoritos() => new(Fabrica, Relogio);

    /// <summary>Semeia a demonstração com uma senha de teste (gerada aqui, nunca a dos user-secrets).</summary>
    public async Task<BancoDeTeste> ComDemoAsync()
    {
        await Semeadura().SemearAsync(SenhaDeTeste);
        return this;
    }

    public const string SenhaDeTeste = "teste-senha-1";

    public ClaimsPrincipal Quem(string email)
    {
        using var db = Db();
        var u = db.Usuarios.Single(x => x.Email == email);
        var papel = u.Papel switch { Papel.Admin => Papeis.Admin, Papel.Organizador => Papeis.Organizador, _ => Papeis.Usuario };
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, u.Id.ToString()), new Claim(ClaimTypes.Role, papel), new Claim(ClaimTypes.Name, u.Nome)], "teste"));
    }

    public void Dispose() => conexao.Dispose();

    private sealed class FabricaSqlite(DbContextOptions<MainContext> opcoes) : IDbContextFactory<MainContext>
    {
        public MainContext CreateDbContext() => new(opcoes);
    }
}
