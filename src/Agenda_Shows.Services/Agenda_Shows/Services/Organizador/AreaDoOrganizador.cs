using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Comum;
using Agenda_Shows.Services.Contas;
using Agenda_Shows.Services.Eventos;
using Agenda_Shows.Services.Pedidos;
using Agenda_Shows.Services.Time;
using Microsoft.EntityFrameworkCore;

namespace Agenda_Shows.Services.Organizador;

public sealed record LinhaDoMeuEvento(int Id, string Slug, string Titulo, string Categoria, string ImagemUrl, DateTime InicioUtc,
    DateTime FimUtc, StatusDoEvento Status, bool Destaque, int Vendidos, int Capacidade, int ReceitaCentavos, int Visualizacoes,
    string Organizador, int OrganizadorId);

public sealed record PainelDoOrganizador(int Eventos, int Publicados, int IngressosVendidos, int ReceitaCentavos, int Visualizacoes,
    IReadOnlyList<(DateOnly Dia, int Vendas, int Visualizacoes)> UltimosDias, IReadOnlyList<LinhaDoMeuEvento> Proximos);

public sealed class LoteForm
{
    public int? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int Quantidade { get; set; } = 100;
}

/// <summary>O formulário de evento do organizador. Hora em Brasília (o que a pessoa digita); o serviço converte para UTC.</summary>
public sealed class EventoForm
{
    public int? Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Resumo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public int CategoriaId { get; set; }
    public string LocalNome { get; set; } = string.Empty;
    public string Endereco { get; set; } = string.Empty;
    public string Bairro { get; set; } = string.Empty;
    public double Latitude { get; set; } = -25.4284;
    public double Longitude { get; set; } = -49.2733;
    public DateTime InicioLocal { get; set; }
    public DateTime FimLocal { get; set; }
    public string ImagemUrl { get; set; } = string.Empty;
    public int ClassificacaoIndicativa { get; set; }
    public bool Publicar { get; set; } = true;
    public List<LoteForm> Lotes { get; set; } = [];
}

public sealed record VendaDoEvento(string Codigo, string Comprador, StatusDoPedido Status, MetodoDePagamento Metodo, int TotalCentavos,
    int Quantidade, DateTime CriadoEmUtc);

public sealed record VendasDoEvento(LinhaDoMeuEvento Evento, IReadOnlyList<VendaDoEvento> Pedidos, int Entraram);

public interface IAreaDoOrganizador
{
    Task<PainelDoOrganizador> PainelAsync(ClaimsPrincipal quem, CancellationToken ct = default);
    Task<IReadOnlyList<LinhaDoMeuEvento>> MeusEventosAsync(ClaimsPrincipal quem, CancellationToken ct = default);
    Task<EventoForm?> ParaEditarAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default);
    Task<Resultado<string>> SalvarAsync(ClaimsPrincipal quem, EventoForm form, CancellationToken ct = default);
    Task<Resultado<bool>> CancelarAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default);
    Task<VendasDoEvento?> VendasAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default);
    Task<Resultado<IngressoDto>> ValidarIngressoAsync(ClaimsPrincipal quem, int eventoId, string codigo, CancellationToken ct = default);
    IReadOnlyList<string> GaleriaDeFotos(string? nicho);
}

/// <summary>
/// A área do organizador: ele só enxerga e mexe nos eventos em que é o <c>OrganizadorId</c>. O admin passa por todas
/// as portas (<see cref="Autorizacao.EnsureDonoDoEvento"/>). Toda consulta daqui filtra pelo dono — não é a tela que esconde.
/// </summary>
public sealed class AreaDoOrganizador(IDbContextFactory<MainContext> fabrica, TimeProvider relogio) : IAreaDoOrganizador
{
    public const int MaximoDeLotes = 8;

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    /// <summary>Os eventos que a pessoa pode ver aqui: os dela; o admin vê todos.</summary>
    private static IQueryable<Evento> Meus(MainContext db, ClaimsPrincipal quem)
    {
        var id = Autorizacao.EnsureOrganizador(quem);
        var q = db.Eventos.AsNoTracking();
        return Autorizacao.EhAdmin(quem) ? q : q.Where(e => e.OrganizadorId == id);
    }

    internal static IQueryable<LinhaDoMeuEvento> Linhas(IQueryable<Evento> q, MainContext db) => q.Select(e => new LinhaDoMeuEvento(
        e.Id, e.Slug, e.Titulo, e.Categoria!.Nome, e.ImagemUrl, e.InicioUtc, e.FimUtc, e.Status, e.Destaque,
        e.Ingressos.Sum(i => (int?)i.Vendidos) ?? 0,
        e.Ingressos.Sum(i => (int?)i.Quantidade) ?? 0,
        db.Pedidos.Where(p => p.EventoId == e.Id && p.Status == StatusDoPedido.Pago).Sum(p => (int?)p.TotalCentavos) ?? 0,
        db.Visualizacoes.Count(v => v.EventoId == e.Id),
        e.Organizador!.NomeDoOrganizador ?? e.Organizador.Nome, e.OrganizadorId));

    public async Task<PainelDoOrganizador> PainelAsync(ClaimsPrincipal quem, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var agora = Agora;
        var meus = Meus(db, quem);
        var ids = await meus.Select(e => e.Id).ToListAsync(ct);
        var linhas = await Linhas(meus, db).ToListAsync(ct);

        var desde = agora.AddDays(-14);
        var vendas = await db.Pedidos.AsNoTracking().Where(p => ids.Contains(p.EventoId) && p.Status == StatusDoPedido.Pago && p.PagoEmUtc >= desde)
            .Select(p => new { p.PagoEmUtc, Qtd = p.Itens.Sum(i => i.Quantidade) }).ToListAsync(ct);
        var views = await db.Visualizacoes.AsNoTracking().Where(v => ids.Contains(v.EventoId) && v.QuandoUtc >= desde)
            .Select(v => v.QuandoUtc).ToListAsync(ct);
        var hoje = BrazilTime.DataEmBrasilia(agora);
        var dias = Enumerable.Range(0, 14).Select(i => hoje.AddDays(i - 13))
            .Select(d => (d,
                vendas.Where(v => BrazilTime.DataEmBrasilia(v.PagoEmUtc!.Value) == d).Sum(v => v.Qtd),
                views.Count(v => BrazilTime.DataEmBrasilia(v) == d)))
            .ToList();

        return new PainelDoOrganizador(
            linhas.Count, linhas.Count(l => l.Status == StatusDoEvento.Publicado),
            linhas.Sum(l => l.Vendidos), linhas.Sum(l => l.ReceitaCentavos), linhas.Sum(l => l.Visualizacoes), dias,
            linhas.Where(l => l.FimUtc > agora && l.Status != StatusDoEvento.Cancelado).OrderBy(l => l.InicioUtc).Take(6).ToList());
    }

    public async Task<IReadOnlyList<LinhaDoMeuEvento>> MeusEventosAsync(ClaimsPrincipal quem, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await Linhas(Meus(db, quem).OrderByDescending(e => e.InicioUtc), db).ToListAsync(ct);
    }

    public async Task<EventoForm?> ParaEditarAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var e = await Meus(db, quem).Include(x => x.Ingressos).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return null;
        return new EventoForm
        {
            Id = e.Id, Titulo = e.Titulo, Resumo = e.Resumo, Descricao = e.Descricao, CategoriaId = e.CategoriaId,
            LocalNome = e.LocalNome, Endereco = e.Endereco, Bairro = e.Bairro, Latitude = e.Latitude, Longitude = e.Longitude,
            InicioLocal = BrazilTime.ToBrasilia(e.InicioUtc), FimLocal = BrazilTime.ToBrasilia(e.FimUtc), ImagemUrl = e.ImagemUrl,
            ClassificacaoIndicativa = e.ClassificacaoIndicativa, Publicar = e.Status == StatusDoEvento.Publicado,
            Lotes = e.Ingressos.OrderBy(i => i.Ordem).Select(i => new LoteForm
            {
                Id = i.Id, Nome = i.Nome, Descricao = i.Descricao, Preco = i.PrecoCentavos / 100m, Quantidade = i.Quantidade,
            }).ToList(),
        };
    }

    public async Task<Resultado<string>> SalvarAsync(ClaimsPrincipal quem, EventoForm f, CancellationToken ct = default)
    {
        var autorId = Autorizacao.EnsureOrganizador(quem);
        if (Validar(f) is { } erro) return Resultado<string>.Falha(erro);

        await using var db = await fabrica.CreateDbContextAsync(ct);
        await PapelNoBanco.EnsureOrganizadorAsync(db, quem, ct);
        if (!await db.Categorias.AnyAsync(c => c.Id == f.CategoriaId, ct)) return Resultado<string>.Falha("Escolha o nicho do evento.");

        var agora = Agora;
        var inicio = BrazilTime.ToUtc(f.InicioLocal);
        var fim = BrazilTime.ToUtc(f.FimLocal);

        Evento e;
        if (f.Id is { } id)
        {
            var achado = await db.Eventos.Include(x => x.Ingressos).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (achado is null) return Resultado<string>.Falha("Evento não encontrado.");
            e = achado;
            await PapelNoBanco.EnsureDonoDoEventoAsync(db, quem, e.OrganizadorId, ct);
            if (e.Status == StatusDoEvento.Cancelado) return Resultado<string>.Falha("Evento cancelado não pode ser editado.");
        }
        else
        {
            e = new Evento { OrganizadorId = autorId, CriadoEmUtc = agora, Slug = await SlugLivreAsync(db, f.Titulo, ct) };
            db.Eventos.Add(e);
        }

        e.Titulo = f.Titulo.Trim();
        e.Resumo = f.Resumo.Trim();
        e.Descricao = f.Descricao.Trim();
        e.CategoriaId = f.CategoriaId;
        e.LocalNome = f.LocalNome.Trim();
        e.Endereco = f.Endereco.Trim();
        e.Bairro = f.Bairro.Trim();
        e.Latitude = f.Latitude;
        e.Longitude = f.Longitude;
        e.InicioUtc = inicio;
        e.FimUtc = fim;
        e.ImagemUrl = f.ImagemUrl;
        e.ClassificacaoIndicativa = f.ClassificacaoIndicativa;
        e.Status = f.Publicar ? StatusDoEvento.Publicado : StatusDoEvento.Rascunho;
        e.AtualizadoEmUtc = agora;
        // Destaque é decisão do admin: o formulário do organizador não o toca.

        // Lotes: atualiza os que vieram com id, cria os novos, remove os que sumiram — mas nunca reduz abaixo do vendido
        // nem apaga lote com venda (quem comprou continua com o ingresso).
        var vindos = f.Lotes.Where(l => l.Id is not null).Select(l => l.Id!.Value).ToHashSet();
        foreach (var velho in e.Ingressos.Where(i => !vindos.Contains(i.Id)).ToList())
        {
            if (velho.Vendidos > 0) return Resultado<string>.Falha($"O lote \"{velho.Nome}\" já tem ingresso vendido e não pode ser removido.");
            e.Ingressos.Remove(velho);
        }
        var ordem = 0;
        foreach (var l in f.Lotes)
        {
            var t = l.Id is { } lid ? e.Ingressos.FirstOrDefault(i => i.Id == lid) : null;
            if (l.Id is not null && t is null) return Resultado<string>.Falha("Lote inválido.");
            if (t is null) { t = new TipoDeIngresso(); e.Ingressos.Add(t); }
            if (l.Quantidade < t.Vendidos) return Resultado<string>.Falha($"O lote \"{t.Nome}\" já vendeu {t.Vendidos}; a quantidade não pode ficar menor.");
            t.Nome = l.Nome.Trim();
            t.Descricao = string.IsNullOrWhiteSpace(l.Descricao) ? null : l.Descricao.Trim();
            t.PrecoCentavos = (int)Math.Round(l.Preco * 100m, MidpointRounding.AwayFromZero);
            t.Quantidade = l.Quantidade;
            t.Ordem = ordem++;
        }

        await db.SaveChangesAsync(ct);
        return Resultado<string>.Sucesso(e.Slug);
    }

    public async Task<Resultado<bool>> CancelarAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var e = await db.Eventos.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return Resultado<bool>.Falha("Evento não encontrado.");
        await PapelNoBanco.EnsureDonoDoEventoAsync(db, quem, e.OrganizadorId, ct);
        e.Status = StatusDoEvento.Cancelado;
        e.AtualizadoEmUtc = Agora;
        await db.SaveChangesAsync(ct);

        // Quem estava no meio da compra não segura reserva de evento cancelado: os pedidos ainda não pagos são cancelados e
        // os ingressos voltam ao lote. Primeiro o status (acima), depois a limpeza — o pagamento confere o status do evento,
        // então nenhum pedido novo passa a Pago entre um passo e o outro.
        var pendentes = await db.Pedidos.Where(p => p.EventoId == id && p.Status == StatusDoPedido.AguardandoPagamento)
            .Select(p => p.Id).ToListAsync(ct);
        foreach (var pedidoId in pendentes)
            await Pedidos.Pedidos.LiberarAsync(db, pedidoId, StatusDoPedido.Cancelado, ct);
        return Resultado<bool>.Sucesso(true);
    }

    public async Task<VendasDoEvento?> VendasAsync(ClaimsPrincipal quem, int id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var linha = await Linhas(Meus(db, quem).Where(e => e.Id == id), db).FirstOrDefaultAsync(ct);
        if (linha is null) return null;
        var pedidos = await db.Pedidos.AsNoTracking().Where(p => p.EventoId == id && p.Status != StatusDoPedido.Expirado)
            .OrderByDescending(p => p.CriadoEmUtc)
            // Nome do comprador, nunca o e-mail: o organizador não precisa do contato de quem comprou para validar ingresso.
            .Select(p => new VendaDoEvento(p.Codigo, p.Usuario!.Nome, p.Status, p.Metodo, p.TotalCentavos, p.Itens.Sum(i => i.Quantidade), p.CriadoEmUtc))
            .Take(300).ToListAsync(ct);
        var entraram = await db.Ingressos.CountAsync(i => i.EventoId == id && i.UsadoEmUtc != null, ct);
        return new VendasDoEvento(linha, pedidos, entraram);
    }

    public async Task<Resultado<IngressoDto>> ValidarIngressoAsync(ClaimsPrincipal quem, int eventoId, string codigo, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var ev = await db.Eventos.Where(e => e.Id == eventoId).Select(e => new { e.OrganizadorId, e.Status }).FirstOrDefaultAsync(ct);
        if (ev is null) return Resultado<IngressoDto>.Falha("Evento não encontrado.");
        await PapelNoBanco.EnsureDonoDoEventoAsync(db, quem, ev.OrganizadorId, ct);
        if (ev.Status == StatusDoEvento.Cancelado) return Resultado<IngressoDto>.Falha("Este evento foi cancelado: nenhum ingresso vale para a entrada.");

        var c = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        var ingresso = await db.Ingressos.FirstOrDefaultAsync(i => i.Codigo == c && i.EventoId == eventoId, ct);
        if (ingresso is null) return Resultado<IngressoDto>.Falha("Ingresso não encontrado para este evento.");
        if (ingresso.UsadoEmUtc is { } quando)
            return Resultado<IngressoDto>.Falha($"Este ingresso já entrou às {BrazilTime.Format(quando, "HH:mm")}.");

        // Entrada exatamente uma vez: o UPDATE só passa se ainda não foi usado.
        var agora = Agora;
        var ok = await db.Ingressos.Where(i => i.Id == ingresso.Id && i.UsadoEmUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.UsadoEmUtc, agora), ct);
        return ok == 1
            ? Resultado<IngressoDto>.Sucesso(new IngressoDto(ingresso.Codigo, ingresso.NomeDoTipo, agora))
            : Resultado<IngressoDto>.Falha("Este ingresso acabou de ser validado.");
    }

    /// <summary>As fotos que o organizador pode escolher (as imagens moram no wwwroot, não no banco).</summary>
    public IReadOnlyList<string> GaleriaDeFotos(string? nicho)
    {
        var todos = Demo.DadosDaDemo.Nichos.Select(n => n.Slug).ToArray();
        var nichos = string.IsNullOrWhiteSpace(nicho) || !todos.Contains(nicho) ? todos : [nicho];
        return nichos.SelectMany(n => Enumerable.Range(1, 5).Select(i => $"/img/eventos/{n}-{i}")).ToList();
    }

    private string? Validar(EventoForm f)
    {
        if (string.IsNullOrWhiteSpace(f.Titulo) || f.Titulo.Trim().Length is < 4 or > 120) return "O título precisa ter de 4 a 120 caracteres.";
        if (string.IsNullOrWhiteSpace(f.Resumo) || f.Resumo.Trim().Length > 200) return "Escreva um resumo de até 200 caracteres.";
        if ((f.Descricao ?? string.Empty).Length > 4000) return "A descrição pode ter até 4000 caracteres.";
        if (string.IsNullOrWhiteSpace(f.LocalNome) || string.IsNullOrWhiteSpace(f.Endereco)) return "Diga o local e o endereço.";
        if (string.IsNullOrWhiteSpace(f.Bairro)) return "Diga o bairro.";
        if (f.Latitude is < -26.2 or > -24.9 || f.Longitude is < -49.9 or > -48.8) return "Marque no mapa um ponto em Curitiba e região.";
        if (f.FimLocal <= f.InicioLocal) return "O fim precisa ser depois do início.";
        if (f.FimLocal - f.InicioLocal > TimeSpan.FromDays(31)) return "Um evento pode durar até 31 dias.";
        if (f.Id is null && BrazilTime.ToUtc(f.FimLocal) <= Agora) return "O evento já teria terminado. Confira as datas.";
        if (!GaleriaDeFotos(null).Contains(f.ImagemUrl)) return "Escolha uma foto da galeria.";
        if (f.ClassificacaoIndicativa is not (0 or 10 or 12 or 14 or 16 or 18)) return "Classificação indicativa inválida.";
        if (f.Lotes.Count is 0 or > MaximoDeLotes) return $"Cadastre de 1 a {MaximoDeLotes} tipos de ingresso.";
        foreach (var l in f.Lotes)
        {
            if (string.IsNullOrWhiteSpace(l.Nome) || l.Nome.Trim().Length > 60) return "Todo ingresso precisa de um nome (até 60 caracteres).";
            if (l.Preco is < 0 or > 5000) return "Preço de ingresso entre R$ 0 e R$ 5.000.";
            if (l.Quantidade is < 1 or > 100000) return "Quantidade de ingressos entre 1 e 100.000.";
        }
        return null;
    }

    private static async Task<string> SlugLivreAsync(MainContext db, string titulo, CancellationToken ct)
    {
        var baseSlug = Codigos.Slug(titulo);
        var slug = baseSlug;
        for (var i = 2; await db.Eventos.AnyAsync(e => e.Slug == slug, ct); i++) slug = $"{baseSlug}-{i}";
        return slug;
    }
}
