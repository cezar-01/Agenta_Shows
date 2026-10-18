using System.Security.Claims;
using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Auth;
using Agenda_Shows.Services.Comum;
using Agenda_Shows.Services.Contas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agenda_Shows.Services.Pedidos;

public sealed record ItemDto(string Nome, int PrecoUnitarioCentavos, int Quantidade)
{
    public int SubtotalCentavos => PrecoUnitarioCentavos * Quantidade;
}

public sealed record IngressoDto(string Codigo, string NomeDoTipo, DateTime? UsadoEmUtc);

public sealed record PedidoDto(
    string Codigo, StatusDoPedido Status, MetodoDePagamento Metodo, int TotalCentavos, DateTime CriadoEmUtc,
    DateTime ExpiraEmUtc, DateTime? PagoEmUtc, string? CartaoFinal,
    string EventoSlug, string EventoTitulo, string EventoImagem, string LocalNome, DateTime EventoInicioUtc,
    DateTime EventoFimUtc, StatusDoEvento EventoStatus, string Comprador, string CompradorEmail,
    IReadOnlyList<ItemDto> Itens, IReadOnlyList<IngressoDto> Ingressos);

/// <summary>O que a pessoa pediu: lote → quantidade.</summary>
public sealed record Escolha(int TipoDeIngressoId, int Quantidade);

/// <summary>O desfecho do cartão de teste. A mensagem é a que a tela mostra.</summary>
public enum DesfechoDoCartao { Aprovado, Recusado, NaoEhCartaoDeTeste, Invalido }

public interface IPedidos
{
    Task<Resultado<string>> CriarAsync(ClaimsPrincipal quem, int eventoId, IReadOnlyList<Escolha> escolhas, CancellationToken ct = default);
    Task<PedidoDto?> ObterAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default);
    Task<IReadOnlyList<PedidoDto>> MeusAsync(ClaimsPrincipal quem, CancellationToken ct = default);
    Task<Resultado<PedidoDto>> PagarPixSimuladoAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default);
    Task<(DesfechoDoCartao Desfecho, string Mensagem)> PagarCartaoSimuladoAsync(ClaimsPrincipal quem, string codigo, string numero,
        string validade, string cvv, CancellationToken ct = default);
    Task<Resultado<PedidoDto>> CancelarAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default);
    Task<int> ExpirarVencidosAsync(CancellationToken ct = default);
}

/// <summary>
/// Pedidos de ingresso com pagamento SIMULADO. Nada é cobrado: o Pix é um QR de demonstração com o botão "Simular
/// pagamento", e o cartão só aceita os números de teste (<see cref="CartaoAprovado"/> aprova, <see cref="CartaoRecusado"/>
/// recusa) — qualquer número de verdade é recusado sem ser guardado.
/// </summary>
public sealed class Pedidos(IDbContextFactory<MainContext> fabrica, TimeProvider relogio, ILogger<Pedidos> log) : IPedidos
{
    public const string CartaoAprovado = "4242424242424242";
    public const string CartaoRecusado = "4000000000000002";
    public const int MaximoPorPedido = 10;
    /// <summary>Teto por pessoa no evento, somando os pedidos pagos e a reserva em aberto (sem ele, uma conta esvazia o lote).</summary>
    public const int MaximoPorPessoa = 10;
    public static readonly TimeSpan PrazoParaPagar = TimeSpan.FromMinutes(15);

    private DateTime Agora => relogio.GetUtcNow().UtcDateTime;

    public async Task<Resultado<string>> CriarAsync(ClaimsPrincipal quem, int eventoId, IReadOnlyList<Escolha> escolhas,
        CancellationToken ct = default)
    {
        var usuarioId = Autorizacao.EnsureAuthenticated(quem);
        var pedidas = (escolhas ?? []).Where(e => e.Quantidade > 0).GroupBy(e => e.TipoDeIngressoId)
            .Select(g => new Escolha(g.Key, g.Sum(x => x.Quantidade))).ToList();
        if (pedidas.Count == 0) return Resultado<string>.Falha("Escolha pelo menos um ingresso.");
        if (pedidas.Sum(e => e.Quantidade) > MaximoPorPedido) return Resultado<string>.Falha($"No máximo {MaximoPorPedido} ingressos por pedido.");

        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var evento = await db.Eventos.AsNoTracking().Include(e => e.Ingressos).FirstOrDefaultAsync(e => e.Id == eventoId, ct);
        if (evento is null || evento.Status != StatusDoEvento.Publicado) return Resultado<string>.Falha("Este evento não está à venda.");
        if (evento.FimUtc <= agora) return Resultado<string>.Falha("Este evento já terminou.");

        var lotes = evento.Ingressos.ToDictionary(t => t.Id);
        if (pedidas.Any(p => !lotes.ContainsKey(p.TipoDeIngressoId))) return Resultado<string>.Falha("Ingresso inválido para este evento.");

        // O preço vem do banco, nunca da tela: o total é calculado aqui.
        var total = pedidas.Sum(p => lotes[p.TipoDeIngressoId].PrecoCentavos * p.Quantidade);
        var gratuito = total == 0;

        var estrategia = db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Uma compra por vez para a mesma pessoa: o UPDATE vazio trava a linha da conta até o commit, então duas abas (ou
            // dois cliques) da mesma pessoa entram em fila — e a contagem abaixo, feita depois da trava, enxerga o que a
            // outra acabou de reservar.
            await db.Usuarios.Where(u => u.Id == usuarioId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CriadoEmUtc, u => u.CriadoEmUtc), ct);

            // Uma reserva em aberto por evento: senão uma conta empilha pedidos de 10 e segura o lote por 15 min, de novo e de novo.
            var pendente = await db.Pedidos
                .Where(p => p.UsuarioId == usuarioId && p.EventoId == eventoId && p.Status == StatusDoPedido.AguardandoPagamento && p.ExpiraEmUtc > agora)
                .Select(p => p.Codigo).FirstOrDefaultAsync(ct);
            if (pendente is not null)
            {
                await tx.RollbackAsync(ct);
                return Resultado<string>.Falha($"Você já tem um pedido esperando pagamento neste evento (código {pendente}). " +
                                               "Pague ou cancele esse pedido em Meus ingressos antes de fazer outro.");
            }
            var jaTem = await db.ItensDoPedido
                .Where(i => i.Pedido!.UsuarioId == usuarioId && i.Pedido.EventoId == eventoId && i.Pedido.Status == StatusDoPedido.Pago)
                .SumAsync(i => (int?)i.Quantidade, ct) ?? 0;
            if (jaTem + pedidas.Sum(p => p.Quantidade) > MaximoPorPessoa)
            {
                await tx.RollbackAsync(ct);
                var resta = Math.Max(0, MaximoPorPessoa - jaTem);
                return Resultado<string>.Falha(resta == 0
                    ? $"Você já tem {jaTem} ingressos para este evento, o máximo por pessoa ({MaximoPorPessoa})."
                    : $"O máximo é {MaximoPorPessoa} ingressos por pessoa neste evento e você já tem {jaTem}: dá para pegar mais {resta}.");
            }

            // Reserva com UPDATE condicional: se duas pessoas disputam o último ingresso, só uma linha é afetada.
            foreach (var p in pedidas)
            {
                var afetadas = await db.TiposDeIngresso
                    .Where(t => t.Id == p.TipoDeIngressoId && t.Vendidos + p.Quantidade <= t.Quantidade)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Vendidos, t => t.Vendidos + p.Quantidade), ct);
                if (afetadas == 0)
                {
                    await tx.RollbackAsync(ct);
                    return Resultado<string>.Falha($"Não há mais \"{lotes[p.TipoDeIngressoId].Nome}\" suficientes. Escolha outra quantidade.");
                }
            }

            var pedido = new Pedido
            {
                Codigo = Codigos.Novo(10), UsuarioId = usuarioId, EventoId = eventoId, TotalCentavos = total, CriadoEmUtc = agora,
                ExpiraEmUtc = agora + PrazoParaPagar,
                Status = gratuito ? StatusDoPedido.Pago : StatusDoPedido.AguardandoPagamento,
                Metodo = gratuito ? MetodoDePagamento.Gratuito : MetodoDePagamento.Nenhum,
                PagoEmUtc = gratuito ? agora : null,
                Itens = pedidas.Select(p => new ItemDoPedido
                {
                    TipoDeIngressoId = p.TipoDeIngressoId, NomeDoTipo = lotes[p.TipoDeIngressoId].Nome,
                    PrecoUnitarioCentavos = lotes[p.TipoDeIngressoId].PrecoCentavos, Quantidade = p.Quantidade,
                }).ToList(),
            };
            if (gratuito) Emitir(pedido);
            db.Pedidos.Add(pedido);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Resultado<string>.Sucesso(pedido.Codigo);
        });
    }

    public async Task<PedidoDto?> ObterAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default)
    {
        var usuarioId = Autorizacao.EnsureAuthenticated(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var q = db.Pedidos.AsNoTracking().Where(p => p.Codigo == codigo);
        // Pedido é de quem comprou (ou do admin). Código de outra pessoa = "não encontrado", nunca "proibido":
        // a resposta não confirma que o código existe.
        if (!Autorizacao.EhAdmin(quem)) q = q.Where(p => p.UsuarioId == usuarioId);
        return await Projetar(q).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<PedidoDto>> MeusAsync(ClaimsPrincipal quem, CancellationToken ct = default)
    {
        var usuarioId = Autorizacao.EnsureAuthenticated(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await Projetar(db.Pedidos.AsNoTracking().Where(p => p.UsuarioId == usuarioId && p.Status != StatusDoPedido.Expirado)
            .OrderByDescending(p => p.CriadoEmUtc)).Take(100).ToListAsync(ct);
    }

    public async Task<Resultado<PedidoDto>> PagarPixSimuladoAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default)
    {
        var r = await ConfirmarPagamentoAsync(quem, codigo, MetodoDePagamento.Pix, null, ct);
        return r;
    }

    public async Task<(DesfechoDoCartao Desfecho, string Mensagem)> PagarCartaoSimuladoAsync(ClaimsPrincipal quem, string codigo,
        string numero, string validade, string cvv, CancellationToken ct = default)
    {
        Autorizacao.EnsureAuthenticated(quem);
        var digitos = new string((numero ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digitos.Length is < 13 or > 19 || !LuhnOk(digitos)) return (DesfechoDoCartao.Invalido, "Número de cartão inválido.");
        if (!ValidadeOk(validade)) return (DesfechoDoCartao.Invalido, "Validade inválida (use MM/AA, no futuro).");
        if (new string((cvv ?? string.Empty).Where(char.IsDigit).ToArray()).Length is < 3 or > 4) return (DesfechoDoCartao.Invalido, "CVV inválido.");

        // É uma simulação: cartão de verdade é recusado sem ser guardado nem registrado.
        if (digitos != CartaoAprovado && digitos != CartaoRecusado)
            return (DesfechoDoCartao.NaoEhCartaoDeTeste, "Isto é uma simulação: use o cartão de teste 4242 4242 4242 4242. Nenhum cartão real é aceito.");
        if (digitos == CartaoRecusado)
            return (DesfechoDoCartao.Recusado, "Pagamento recusado pelo banco (simulação). Tente o cartão 4242 4242 4242 4242 ou o Pix.");

        var r = await ConfirmarPagamentoAsync(quem, codigo, MetodoDePagamento.Cartao, digitos[^4..], ct);
        return r.Ok ? (DesfechoDoCartao.Aprovado, "Pagamento aprovado!") : (DesfechoDoCartao.Invalido, r.Erro!);
    }

    public async Task<Resultado<PedidoDto>> CancelarAsync(ClaimsPrincipal quem, string codigo, CancellationToken ct = default)
    {
        var usuarioId = Autorizacao.EnsureAuthenticated(quem);
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var pedido = await db.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Codigo == codigo && p.UsuarioId == usuarioId, ct);
        if (pedido is null) return Resultado<PedidoDto>.Falha("Pedido não encontrado.");
        if (pedido.Status != StatusDoPedido.AguardandoPagamento) return Resultado<PedidoDto>.Falha("Só dá para cancelar um pedido que ainda não foi pago.");

        if (!await LiberarAsync(db, pedido.Id, StatusDoPedido.Cancelado, ct)) return Resultado<PedidoDto>.Falha("O pedido mudou de situação. Recarregue a página.");
        return Resultado<PedidoDto>.Sucesso((await ObterAsync(quem, codigo, ct))!);
    }

    /// <summary>O job: pedido que passou do prazo sem pagar volta os ingressos ao lote. Devolve quantos expirou.</summary>
    public async Task<int> ExpirarVencidosAsync(CancellationToken ct = default)
    {
        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var vencidos = await db.Pedidos.Where(p => p.Status == StatusDoPedido.AguardandoPagamento && p.ExpiraEmUtc <= agora)
            .OrderBy(p => p.ExpiraEmUtc).Select(p => p.Id).Take(200).ToListAsync(ct);
        var n = 0;
        foreach (var id in vencidos)
            if (await LiberarAsync(db, id, StatusDoPedido.Expirado, ct)) n++;
        if (n > 0) log.LogInformation("Pedidos: {N} reservas expiradas devolvidas ao lote.", n);
        return n;
    }

    /// <summary>
    /// Troca AguardandoPagamento → <paramref name="novo"/> e devolve os ingressos ao lote, na mesma transação. A troca é
    /// condicional (WHERE Status = Aguardando): o job e o cancelamento disputando o mesmo pedido devolvem uma vez só.
    /// </summary>
    internal static async Task<bool> LiberarAsync(MainContext db, int pedidoId, StatusDoPedido novo, CancellationToken ct)
    {
        var estrategia = db.Database.CreateExecutionStrategy();
        return await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var trocou = await db.Pedidos.Where(p => p.Id == pedidoId && p.Status == StatusDoPedido.AguardandoPagamento)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, novo), ct);
            if (trocou == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            var itens = await db.ItensDoPedido.AsNoTracking().Where(i => i.PedidoId == pedidoId).ToListAsync(ct);
            foreach (var i in itens)
                await db.TiposDeIngresso.Where(t => t.Id == i.TipoDeIngressoId)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.Vendidos, t => t.Vendidos - i.Quantidade), ct);
            await tx.CommitAsync(ct);
            return true;
        });
    }

    private async Task<Resultado<PedidoDto>> ConfirmarPagamentoAsync(ClaimsPrincipal quem, string codigo, MetodoDePagamento metodo,
        string? final, CancellationToken ct)
    {
        var usuarioId = Autorizacao.EnsureAuthenticated(quem);
        var agora = Agora;
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var pedido = await db.Pedidos.AsNoTracking().Include(p => p.Itens)
            .FirstOrDefaultAsync(p => p.Codigo == codigo && p.UsuarioId == usuarioId, ct);
        if (pedido is null) return Resultado<PedidoDto>.Falha("Pedido não encontrado.");

        // A troca para Pago é CONDICIONAL (ainda aguardando, dentro do prazo e com o evento ainda à venda): se o job de
        // expiração devolveu os ingressos ao lote um instante antes, ou se o organizador cancelou o evento, o pagamento não
        // passa — nunca fica um pedido pago com o lote já devolvido, nem ingresso pago de evento cancelado. E o clique duplo:
        // só um dos dois troca; o outro encontra Pago e responde o mesmo sucesso, sem emitir de novo.
        var estrategia = db.Database.CreateExecutionStrategy();
        var pago = await estrategia.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var trocou = await db.Pedidos
                .Where(p => p.Id == pedido.Id && p.Status == StatusDoPedido.AguardandoPagamento && p.ExpiraEmUtc > agora
                            && db.Eventos.Any(e => e.Id == p.EventoId && e.Status == StatusDoEvento.Publicado && e.FimUtc > agora))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.Status, StatusDoPedido.Pago)
                    .SetProperty(p => p.Metodo, metodo)
                    .SetProperty(p => p.PagoEmUtc, agora)
                    .SetProperty(p => p.CartaoFinal, final), ct);
            if (trocou == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }
            foreach (var i in pedido.Itens)
                for (var n = 0; n < i.Quantidade; n++)
                    db.Ingressos.Add(new Ingresso { PedidoId = pedido.Id, EventoId = pedido.EventoId, Codigo = Codigos.Novo(10), NomeDoTipo = i.NomeDoTipo });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        });

        if (!pago)
        {
            var s = await db.Pedidos.AsNoTracking().Where(p => p.Id == pedido.Id)
                .Select(p => new { p.Status, p.ExpiraEmUtc, EventoStatus = p.Evento!.Status, EventoFim = p.Evento.FimUtc }).FirstAsync(ct);
            if (s.Status != StatusDoPedido.Pago)
                return Resultado<PedidoDto>.Falha(
                    s.EventoStatus == StatusDoEvento.Cancelado ? "Este evento foi cancelado: o pedido não pode ser pago (nada foi cobrado)."
                    : s.Status == StatusDoPedido.AguardandoPagamento && s.ExpiraEmUtc > agora
                        ? (s.EventoFim <= agora ? "Este evento já terminou." : "Este evento não está mais à venda.")
                    : s.Status is StatusDoPedido.AguardandoPagamento or StatusDoPedido.Expirado ? "O prazo para pagar acabou. Faça um novo pedido."
                    : "Este pedido não pode mais ser pago.");
        }
        return Resultado<PedidoDto>.Sucesso((await ObterAsync(quem, codigo, ct))!);
    }

    /// <summary>Um ingresso por pessoa, cada um com o seu código — é o que vira QR e o que a porta do evento valida.</summary>
    private static void Emitir(Pedido p)
    {
        if (p.IngressosEmitidos.Count > 0) return;
        foreach (var i in p.Itens)
            for (var n = 0; n < i.Quantidade; n++)
                p.IngressosEmitidos.Add(new Ingresso { EventoId = p.EventoId, Codigo = Codigos.Novo(10), NomeDoTipo = i.NomeDoTipo });
    }

    internal static IQueryable<PedidoDto> Projetar(IQueryable<Pedido> q) => q.Select(p => new PedidoDto(
        p.Codigo, p.Status, p.Metodo, p.TotalCentavos, p.CriadoEmUtc, p.ExpiraEmUtc, p.PagoEmUtc, p.CartaoFinal,
        p.Evento!.Slug, p.Evento.Titulo, p.Evento.ImagemUrl, p.Evento.LocalNome, p.Evento.InicioUtc, p.Evento.FimUtc, p.Evento.Status,
        p.Usuario!.Nome, p.Usuario.Email,
        p.Itens.Select(i => new ItemDto(i.NomeDoTipo, i.PrecoUnitarioCentavos, i.Quantidade)).ToList(),
        p.IngressosEmitidos.OrderBy(i => i.Id).Select(i => new IngressoDto(i.Codigo, i.NomeDoTipo, i.UsadoEmUtc)).ToList()));

    public static bool LuhnOk(string digitos)
    {
        var soma = 0;
        var dobra = false;
        for (var i = digitos.Length - 1; i >= 0; i--)
        {
            var d = digitos[i] - '0';
            if (dobra) { d *= 2; if (d > 9) d -= 9; }
            soma += d;
            dobra = !dobra;
        }
        return soma % 10 == 0;
    }

    private bool ValidadeOk(string? validade)
    {
        var v = (validade ?? string.Empty).Replace(" ", "");
        var partes = v.Split('/');
        if (partes.Length != 2 || !int.TryParse(partes[0], out var mes) || !int.TryParse(partes[1], out var ano)) return false;
        if (mes is < 1 or > 12) return false;
        if (ano < 100) ano += 2000;
        var agora = Agora;
        return ano > agora.Year || (ano == agora.Year && mes >= agora.Month);
    }
}
