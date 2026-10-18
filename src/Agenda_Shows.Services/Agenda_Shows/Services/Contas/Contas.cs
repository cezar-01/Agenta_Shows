using System.Net.Mail;
using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Services.Contas;

public sealed record UsuarioDto(int Id, string Nome, string Email, Papel Papel, string? NomeDoOrganizador, DateTime CriadoEmUtc);

public sealed record Cadastro(string Nome, string Email, string Senha, bool QueroOrganizar, string? NomeDoOrganizador);

/// <summary>O resultado de um gesto: deu certo, ou a mensagem que a tela mostra (nunca uma exceção crua).</summary>
public sealed record Resultado<T>(T? Valor, string? Erro)
{
    public bool Ok => Erro is null;
    public static Resultado<T> Sucesso(T valor) => new(valor, null);
    public static Resultado<T> Falha(string erro) => new(default, erro);
}

public interface IContas
{
    Task<Resultado<UsuarioDto>> CadastrarAsync(Cadastro c, CancellationToken ct = default);
    Task<UsuarioDto?> AutenticarAsync(string email, string senha, CancellationToken ct = default);
    Task<UsuarioDto?> ObterAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<UsuarioDto>> ListarAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default);
    Task<Resultado<UsuarioDto>> MudarPapelAsync(ClaimsPrincipal quem, int usuarioId, Papel papel, CancellationToken ct = default);
}

public sealed class Contas : IContas
{
    public const int SenhaMinima = 8;

    private readonly IDbContextFactory<MainContext> fabrica;
    private readonly IPasswordHasher<Usuario> hasher;
    private readonly TimeProvider relogio;

    // Hash de uma senha que ninguém sabe, calculado uma vez: o login de e-mail inexistente também verifica um hash,
    // então o tempo de resposta não diz se a conta existe (anti-enumeração, molde do MyPods).
    private readonly string hashFalso;

    public Contas(IDbContextFactory<MainContext> fabrica, IPasswordHasher<Usuario> hasher, TimeProvider relogio)
    {
        this.fabrica = fabrica;
        this.hasher = hasher;
        this.relogio = relogio;
        hashFalso = hasher.HashPassword(new Usuario(), Comum.Codigos.Novo(24));
    }

    public static string NormalizaEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static string? ValidaSenha(string? senha)
    {
        if (string.IsNullOrEmpty(senha) || senha.Length < SenhaMinima) return $"A senha precisa de pelo menos {SenhaMinima} caracteres.";
        if (!senha.Any(char.IsLetter) || !senha.Any(char.IsDigit)) return "Use letras e números na senha.";
        return null;
    }

    public async Task<Resultado<UsuarioDto>> CadastrarAsync(Cadastro c, CancellationToken ct = default)
    {
        var nome = (c.Nome ?? string.Empty).Trim();
        var email = NormalizaEmail(c.Email);
        if (nome.Length is < 2 or > 120) return Resultado<UsuarioDto>.Falha("Digite seu nome (de 2 a 120 letras).");
        if (email.Length > 200 || !MailAddress.TryCreate(email, out _)) return Resultado<UsuarioDto>.Falha("Digite um e-mail válido.");
        if (ValidaSenha(c.Senha) is { } erroDeSenha) return Resultado<UsuarioDto>.Falha(erroDeSenha);

        var nomeOrg = (c.NomeDoOrganizador ?? string.Empty).Trim();
        if (c.QueroOrganizar && nomeOrg.Length is < 2 or > 120)
            return Resultado<UsuarioDto>.Falha("Diga o nome do coletivo, produtora ou espaço que vai divulgar os eventos.");

        await using var db = await fabrica.CreateDbContextAsync(ct);
        // Escolha consciente: o cadastro diz que o e-mail já tem conta (o login não diz). Esconder isso exigiria confirmar o
        // e-mail por mensagem, que a demonstração não envia; a enumeração fica limitada pelo rate limit "login" (10/min por IP).
        if (await db.Usuarios.AnyAsync(u => u.Email == email, ct))
            return Resultado<UsuarioDto>.Falha("Já existe uma conta com esse e-mail. Entre com ela.");

        var u = new Usuario
        {
            Nome = nome, Email = email, CriadoEmUtc = relogio.GetUtcNow().UtcDateTime,
            // Cadastro aberto de organizador: é uma demonstração. Admin nunca nasce pelo cadastro.
            Papel = c.QueroOrganizar ? Papel.Organizador : Papel.Usuario,
            NomeDoOrganizador = c.QueroOrganizar ? nomeOrg : null,
        };
        u.SenhaHash = hasher.HashPassword(u, c.Senha);
        db.Usuarios.Add(u);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dois cadastros com o mesmo e-mail ao mesmo tempo: o índice único decide, a mensagem é a mesma.
            return Resultado<UsuarioDto>.Falha("Já existe uma conta com esse e-mail. Entre com ela.");
        }
        return Resultado<UsuarioDto>.Sucesso(Dto(u));
    }

    public async Task<UsuarioDto?> AutenticarAsync(string email, string senha, CancellationToken ct = default)
    {
        var e = NormalizaEmail(email);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == e, ct);
        var hash = u?.SenhaHash ?? hashFalso;
        var r = hasher.VerifyHashedPassword(u ?? new Usuario(), hash, senha ?? string.Empty);
        if (u is null || r == PasswordVerificationResult.Failed) return null;
        if (r == PasswordVerificationResult.SuccessRehashNeeded)
        {
            u.SenhaHash = hasher.HashPassword(u, senha!);
            await db.SaveChangesAsync(ct);
        }
        return Dto(u);
    }

    public async Task<UsuarioDto?> ObterAsync(int id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var u = await db.Usuarios.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return u is null ? null : Dto(u);
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(ClaimsPrincipal quem, string? busca, CancellationToken ct = default)
    {
        Autorizacao.EnsureAdmin(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var q = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            var t = busca.Trim();
            q = q.Where(u => u.Nome.Contains(t) || u.Email.Contains(t) || (u.NomeDoOrganizador != null && u.NomeDoOrganizador.Contains(t)));
        }
        return await q.OrderByDescending(u => u.Papel).ThenBy(u => u.Nome).Take(200)
            .Select(u => new UsuarioDto(u.Id, u.Nome, u.Email, u.Papel, u.NomeDoOrganizador, u.CriadoEmUtc)).ToListAsync(ct);
    }

    public async Task<Resultado<UsuarioDto>> MudarPapelAsync(ClaimsPrincipal quem, int usuarioId, Papel papel, CancellationToken ct = default)
    {
        var meuId = Autorizacao.EnsureAdmin(quem);
        if (!Enum.IsDefined(papel)) return Resultado<UsuarioDto>.Falha("Papel inválido.");
        // O admin não se rebaixa: um clique errado deixaria o sistema sem ninguém que possa desfazer.
        if (usuarioId == meuId && papel != Papel.Admin) return Resultado<UsuarioDto>.Falha("Você não pode tirar o seu próprio acesso de admin.");

        await using var db = await fabrica.CreateDbContextAsync(ct);
        // Quem age precisa ser admin NO BANCO: um admin rebaixado com /admin/usuarios ainda aberto não se promove de volta.
        await PapelNoBanco.EnsureAdminAsync(db, quem, ct);
        var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, ct);
        if (u is null) return Resultado<UsuarioDto>.Falha("Conta não encontrada.");
        u.Papel = papel;
        if (papel != Papel.Usuario && string.IsNullOrWhiteSpace(u.NomeDoOrganizador)) u.NomeDoOrganizador = u.Nome;
        await db.SaveChangesAsync(ct);
        return Resultado<UsuarioDto>.Sucesso(Dto(u));
    }

    private static UsuarioDto Dto(Usuario u) => new(u.Id, u.Nome, u.Email, u.Papel, u.NomeDoOrganizador, u.CriadoEmUtc);
}
