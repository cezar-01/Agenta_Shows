using System.Security.Claims;
using Agenda_Shows.Services.Auth;

namespace Agenda_Shows.Tests.Auth;

public class AutorizacaoTests
{
    internal static ClaimsPrincipal Pessoa(string? papel = null, int? id = 1, bool autenticado = true)
    {
        var claims = new List<Claim>();
        if (papel is not null) claims.Add(new Claim(ClaimTypes.Role, papel));
        if (id is { } i) claims.Add(new Claim(ClaimTypes.NameIdentifier, i.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, autenticado ? "teste" : null));
    }

    [Fact]
    public void QuemNaoEntrou_OuveQuePrecisaEntrar_NuncaOPapel()
    {
        var ex = Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureOrganizador(Pessoa(Papeis.Organizador, autenticado: false)));
        Assert.Equal(Autorizacao.MensagemAutenticacao, ex.Message);

        ex = Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureAdmin(null));
        Assert.Equal(Autorizacao.MensagemAutenticacao, ex.Message);
    }

    [Fact]
    public void UsuarioComum_NaoEhOrganizador_NemAdmin()
    {
        var ana = Pessoa(Papeis.Usuario);
        Assert.Equal(Autorizacao.MensagemOrganizador, Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureOrganizador(ana)).Message);
        Assert.Equal(Autorizacao.MensagemAdmin, Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureAdmin(ana)).Message);
    }

    [Fact]
    public void Organizador_NaoEhAdmin()
    {
        Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureAdmin(Pessoa(Papeis.Organizador)));
    }

    [Fact]
    public void Admin_TambemOrganiza()
    {
        Assert.Equal(1, Autorizacao.EnsureOrganizador(Pessoa(Papeis.Admin)));
        Assert.True(Autorizacao.EhOrganizador(Pessoa(Papeis.Admin)));
    }

    [Fact]
    public void SoODonoMexeNoEvento_OuOAdmin()
    {
        Autorizacao.EnsureDonoDoEvento(Pessoa(Papeis.Organizador, id: 7), organizadorIdDoEvento: 7);
        Autorizacao.EnsureDonoDoEvento(Pessoa(Papeis.Admin, id: 1), organizadorIdDoEvento: 7);

        var ex = Assert.Throws<UnauthorizedAccessException>(() =>
            Autorizacao.EnsureDonoDoEvento(Pessoa(Papeis.Organizador, id: 8), organizadorIdDoEvento: 7));
        Assert.Equal(Autorizacao.MensagemDono, ex.Message);
    }

    [Fact]
    public void SessaoSemId_NaoEhNinguem()
    {
        Assert.Null(Autorizacao.UsuarioId(Pessoa(Papeis.Admin, id: null)));
        Assert.Throws<UnauthorizedAccessException>(() => Autorizacao.EnsureAdmin(Pessoa(Papeis.Admin, id: null)));
    }
}
