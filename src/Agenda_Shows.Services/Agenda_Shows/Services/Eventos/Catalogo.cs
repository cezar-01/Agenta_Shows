using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Time;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Services.Eventos;

public sealed record CategoriaDto(int Id, string Slug, string Nome, string Descricao, string ImagemUrl, int Proximos);

/// <summary>O card do evento: tudo o que a vitrine, o nicho, a busca e o mapa mostram.</summary>
public sealed record EventoCard(
    int Id, string Slug, string Titulo, string Resumo, string CategoriaSlug, string CategoriaNome,
    string LocalNome, string Bairro, double Latitude, double Longitude,
    DateTime InicioUtc, DateTime FimUtc, string ImagemUrl, int? MenorPrecoCentavos, bool VariosPrecos,
    bool Destaque, StatusDoEvento Status, bool Esgotado, string Organizador)
{
    public bool Gratuito => MenorPrecoCentavos is null or 0 && !VariosPrecos;

    public SituacaoDoEvento SituacaoEm(DateTime agoraUtc) => Situacao.De(InicioUtc, FimUtc, Status, agoraUtc);

    public string RotuloEm(DateTime agoraUtc) => Situacao.Rotulo(InicioUtc, FimUtc, Status, agoraUtc);

    public string Preco => Dinheiro.APartirDe(MenorPrecoCentavos, VariosPrecos);
}

public sealed record Vitrine(
    IReadOnlyList<CategoriaDto> Nichos, IReadOnlyList<EventoCard> AoVivo, IReadOnlyList<EventoCard> Hoje,
    IReadOnlyList<EventoCard> Destaques, IReadOnlyList<EventoCard> Gratis, IReadOnlyList<EventoCard> FimDeSemana,
    IReadOnlyList<(CategoriaDto Nicho, IReadOnlyList<EventoCard> Eventos)> PorNicho);

public sealed record PaginaDoNicho(CategoriaDto Nicho, IReadOnlyList<EventoCard> AoVivo, IReadOnlyList<EventoCard> Hoje,
    IReadOnlyList<EventoCard> Proximos);

public enum Quando { Todos, AoVivo, Hoje, FimDeSemana, Semana }

public sealed record FiltroDeBusca(string? Texto = null, string? Nicho = null, Quando Quando = Quando.Todos, bool SoGratis = false,
    int Pagina = 1, int PorPagina = 18);

public sealed record ResultadoDaBusca(IReadOnlyList<EventoCard> Itens, int Total, int Pagina, int PorPagina)
{
    public bool TemMais => Pagina * PorPagina < Total;
}

public sealed record LoteDto(int Id, string Nome, string? Descricao, int PrecoCentavos, int Disponiveis)
{
    public bool Esgotado => Disponiveis <= 0;
}

public sealed record EventoDetalhe(EventoCard Card, string Descricao, string Endereco, int ClassificacaoIndicativa,
    IReadOnlyList<LoteDto> Lotes, int OrganizadorId, bool Favorito, IReadOnlyList<EventoCard> Relacionados);

/// <summary>A leitura pública: vitrine, nichos, busca, página do evento e pinos do mapa. Só eventos publicados.</summary>
public interface ICatalogo
{
    Task<IReadOnlyList<CategoriaDto>> NichosAsync(CancellationToken ct = default);
    Task<Vitrine> VitrineAsync(CancellationToken ct = default);
    Task<PaginaDoNicho?> NichoAsync(string slug, CancellationToken ct = default);
    Task<ResultadoDaBusca> BuscarAsync(FiltroDeBusca filtro, CancellationToken ct = default);
    Task<EventoDetalhe?> EventoAsync(string slug, int? usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<EventoCard>> MapaAsync(string? nicho, CancellationToken ct = default);
}

public sealed class Catalogo(IDbContextFactory<MainContext> fabrica, TimeProvider relogio) : ICatalogo
{
    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    /// <summary>O que o público enxerga: publicado e ainda não terminado (o cancelado some da vitrine).</summary>
    private static IQueryable<Evento> Visiveis(MainContext db, DateTime agora) =>
        db.Eventos.AsNoTracking().Where(e => e.Status == StatusDoEvento.Publicado && e.FimUtc > agora);

    internal static IQueryable<EventoCard> Cards(IQueryable<Evento> q) => q.Select(e => new EventoCard(
        e.Id, e.Slug, e.Titulo, e.Resumo, e.Categoria!.Slug, e.Categoria.Nome, e.LocalNome, e.Bairro, e.Latitude, e.Longitude,
        e.InicioUtc, e.FimUtc, e.ImagemUrl,
        e.Ingressos.Min(i => (int?)i.PrecoCentavos),
        // Max ≠ Min em vez de Distinct().Count(): sai como dois agregados simples, sem tabela derivada correlacionada.
        e.Ingressos.Max(i => (int?)i.PrecoCentavos) != e.Ingressos.Min(i => (int?)i.PrecoCentavos),
        e.Destaque, e.Status,
        e.Ingressos.All(i => i.Vendidos >= i.Quantidade),
        e.Organizador!.NomeDoOrganizador ?? e.Organizador.Nome));

    public async Task<IReadOnlyList<CategoriaDto>> NichosAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var agora = Agora;
        return await db.Categorias.AsNoTracking().OrderBy(c => c.Ordem)
            .Select(c => new CategoriaDto(c.Id, c.Slug, c.Nome, c.Descricao, c.ImagemUrl,
                c.Eventos.Count(e => e.Status == StatusDoEvento.Publicado && e.FimUtc > agora)))
            .ToListAsync(ct);
    }

    public async Task<Vitrine> VitrineAsync(CancellationToken ct = default)
    {
        var agora = Agora;
        var nichos = await NichosAsync(ct);
        await using var db = await fabrica.CreateDbContextAsync(ct);

        // Os próximos 8 dias cabem inteiros na memória (dezenas de eventos): uma consulta, e as faixas são recortes dela.
        var ate = BrazilTime.InicioDoDiaUtc(BrazilTime.DataEmBrasilia(agora).AddDays(8));
        var proximos = await Cards(Visiveis(db, agora).Where(e => e.InicioUtc < ate).OrderBy(e => e.InicioUtc)).ToListAsync(ct);

        var aoVivo = proximos.Where(e => e.SituacaoEm(agora) == SituacaoDoEvento.AoVivo).OrderByDescending(e => e.Destaque).ThenBy(e => e.FimUtc).ToList();
        var hoje = proximos.Where(e => e.SituacaoEm(agora) == SituacaoDoEvento.Hoje).ToList();
        var destaques = proximos.Where(e => e.Destaque).Take(8).ToList();
        var gratis = proximos.Where(e => e.Gratuito).Take(10).ToList();
        var (fdsDe, fdsAte) = QuandoDoEvento.FimDeSemana(agora);
        var fds = proximos.Where(e => e.InicioUtc < fdsAte && e.FimUtc > fdsDe && e.SituacaoEm(agora) != SituacaoDoEvento.AoVivo).Take(10).ToList();

        var porNicho = nichos
            .Select(n => (n, (IReadOnlyList<EventoCard>)proximos.Where(e => e.CategoriaSlug == n.Slug).Take(8).ToList()))
            .Where(x => x.Item2.Count > 0)
            .ToList();

        return new Vitrine(nichos, aoVivo, hoje, destaques, gratis, fds, porNicho);
    }

    public async Task<PaginaDoNicho?> NichoAsync(string slug, CancellationToken ct = default)
    {
        var nicho = (await NichosAsync(ct)).FirstOrDefault(n => n.Slug == slug);
        if (nicho is null) return null;

        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var eventos = await Cards(Visiveis(db, agora).Where(e => e.Categoria!.Slug == slug).OrderBy(e => e.InicioUtc)).ToListAsync(ct);
        return new PaginaDoNicho(nicho,
            eventos.Where(e => e.SituacaoEm(agora) == SituacaoDoEvento.AoVivo).ToList(),
            eventos.Where(e => e.SituacaoEm(agora) == SituacaoDoEvento.Hoje).ToList(),
            eventos.Where(e => e.SituacaoEm(agora) == SituacaoDoEvento.EmBreve).ToList());
    }

    public async Task<ResultadoDaBusca> BuscarAsync(FiltroDeBusca f, CancellationToken ct = default)
    {
        var agora = Agora;
        var porPagina = Math.Clamp(f.PorPagina, 1, 60);
        var pagina = Math.Max(1, f.Pagina);
        await using var db = await fabrica.CreateDbContextAsync(ct);

        var q = Visiveis(db, agora);
        if (!string.IsNullOrWhiteSpace(f.Nicho)) q = q.Where(e => e.Categoria!.Slug == f.Nicho);
        if (f.SoGratis) q = q.Where(e => e.Ingressos.All(i => i.PrecoCentavos == 0));
        if (!string.IsNullOrWhiteSpace(f.Texto))
        {
            var t = f.Texto.Trim();
            if (t.Length > 80) t = t[..80];
            q = q.Where(e => e.Titulo.Contains(t) || e.Resumo.Contains(t) || e.LocalNome.Contains(t) || e.Bairro.Contains(t)
                             || e.Categoria!.Nome.Contains(t));
        }
        switch (f.Quando)
        {
            case Quando.AoVivo:
                q = q.Where(e => e.InicioUtc <= agora);
                break;
            case Quando.Hoje:
                {
                    var (_, ate) = QuandoDoEvento.Hoje(agora);
                    q = q.Where(e => e.InicioUtc < ate);
                    break;
                }
            case Quando.FimDeSemana:
                {
                    var (de, ate) = QuandoDoEvento.FimDeSemana(agora);
                    q = q.Where(e => e.InicioUtc < ate && e.FimUtc > de);
                    break;
                }
            case Quando.Semana:
                {
                    var (_, ate) = QuandoDoEvento.ProximosSeteDias(agora);
                    q = q.Where(e => e.InicioUtc < ate);
                    break;
                }
        }

        var total = await q.CountAsync(ct);
        var itens = await Cards(q.OrderBy(e => e.InicioUtc).ThenBy(e => e.Id).Skip((pagina - 1) * porPagina).Take(porPagina)).ToListAsync(ct);
        return new ResultadoDaBusca(itens, total, pagina, porPagina);
    }

    public async Task<EventoDetalhe?> EventoAsync(string slug, int? usuarioId, CancellationToken ct = default)
    {
        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        // A página do evento abre também depois de encerrado ou cancelado (quem comprou volta nela); rascunho, não.
        var q = db.Eventos.AsNoTracking().Where(e => e.Slug == slug && e.Status != StatusDoEvento.Rascunho);
        var card = await Cards(q).FirstOrDefaultAsync(ct);
        if (card is null) return null;

        var extra = await q.Select(e => new { e.Descricao, e.Endereco, e.ClassificacaoIndicativa, e.OrganizadorId }).FirstAsync(ct);
        var lotes = await db.TiposDeIngresso.AsNoTracking().Where(t => t.EventoId == card.Id).OrderBy(t => t.Ordem)
            .Select(t => new LoteDto(t.Id, t.Nome, t.Descricao, t.PrecoCentavos, t.Quantidade - t.Vendidos)).ToListAsync(ct);
        var favorito = usuarioId is { } uid && await db.Favoritos.AnyAsync(f => f.UsuarioId == uid && f.EventoId == card.Id, ct);
        var relacionados = await Cards(Visiveis(db, agora).Where(e => e.Categoria!.Slug == card.CategoriaSlug && e.Id != card.Id)
            .OrderBy(e => e.InicioUtc).Take(6)).ToListAsync(ct);

        return new EventoDetalhe(card, extra.Descricao, extra.Endereco, extra.ClassificacaoIndicativa, lotes, extra.OrganizadorId,
            favorito, relacionados);
    }

    public async Task<IReadOnlyList<EventoCard>> MapaAsync(string? nicho, CancellationToken ct = default)
    {
        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var (_, ate) = QuandoDoEvento.ProximosSeteDias(agora);
        var q = Visiveis(db, agora).Where(e => e.InicioUtc < ate);
        if (!string.IsNullOrWhiteSpace(nicho)) q = q.Where(e => e.Categoria!.Slug == nicho);
        return await Cards(q.OrderBy(e => e.InicioUtc)).ToListAsync(ct);
    }
}
