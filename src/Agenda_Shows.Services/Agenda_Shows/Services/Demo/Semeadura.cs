using Agenda_Shows.Contexts;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Comum;
using Agenda_Shows.Services.Time;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Agenda_Shows.Services.Demo;

/// <summary>
/// Planta os dados de demonstração (<see cref="DadosDaDemo"/>) num banco vazio e, em dev, mantém as datas atuais a cada
/// boot. Cada parte só roda se a tabela dela estiver vazia: rodar duas vezes não duplica nada.
/// </summary>
public sealed class Semeadura(
    IDbContextFactory<MainContext> fabrica,
    IPasswordHasher<Usuario> hasher,
    TimeProvider relogio,
    ILogger<Semeadura> log)
{
    /// <summary>Quantas pessoas "de público" (contas sem senha conhecida) compram ingresso na demonstração.</summary>
    public const int PessoasDoPublico = 24;

    /// <param name="senhaDasContas">
    /// A senha das contas de demonstração, lida dos user-secrets (<see cref="DadosDaDemo.ChaveDaSenha"/>). Nula: as
    /// contas nascem com senha aleatória que ninguém conhece (os eventos precisam de organizador, mas ninguém entra).
    /// </param>
    public async Task SemearAsync(string? senhaDasContas, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow().UtcDateTime;

        if (!await db.Categorias.AnyAsync(ct))
        {
            db.Categorias.AddRange(DadosDaDemo.Nichos.Select(n => new Categoria
            {
                Slug = n.Slug, Nome = n.Nome, Descricao = n.Descricao, Ordem = n.Ordem, ImagemUrl = $"/img/nichos/{n.Slug}.webp",
            }));
            await db.SaveChangesAsync(ct);
            log.LogInformation("Semeadura: {N} nichos criados.", DadosDaDemo.Nichos.Length);
        }

        var semSenha = string.IsNullOrWhiteSpace(senhaDasContas);
        if (semSenha)
            log.LogWarning("Semeadura: sem {Chave} nos user-secrets — as contas de demonstração nascem com senha aleatória " +
                           "(ninguém consegue entrar nelas). Veja docker/mysql/README.md.", DadosDaDemo.ChaveDaSenha);

        foreach (var c in DadosDaDemo.Contas)
        {
            if (await db.Usuarios.AnyAsync(u => u.Email == c.Email, ct)) continue;
            var u = new Usuario { Email = c.Email, Nome = c.Nome, Papel = c.Papel, NomeDoOrganizador = c.NomeDoOrganizador, CriadoEmUtc = agora };
            u.SenhaHash = hasher.HashPassword(u, semSenha ? Codigos.Novo(32) : senhaDasContas!);
            db.Usuarios.Add(u);
        }
        await db.SaveChangesAsync(ct);

        if (!await db.Eventos.AnyAsync(ct))
        {
            var categorias = await db.Categorias.ToDictionaryAsync(c => c.Slug, c => c.Id, ct);
            var organizadores = await db.Usuarios.Where(u => u.Papel != Papel.Usuario).ToDictionaryAsync(u => u.Email, u => u.Id, ct);
            foreach (var e in DadosDaDemo.Eventos)
            {
                var (ini, fim) = Datas(e, agora);
                db.Eventos.Add(new Evento
                {
                    Slug = e.Slug, Titulo = e.Titulo, Resumo = e.Resumo, Descricao = e.Descricao,
                    CategoriaId = categorias[e.Nicho], OrganizadorId = organizadores[e.Organizador],
                    LocalNome = e.Local, Endereco = e.Endereco, Bairro = e.Bairro, Latitude = e.Lat, Longitude = e.Lng,
                    InicioUtc = ini, FimUtc = fim, ImagemUrl = $"/img/eventos/{e.Foto}",
                    ClassificacaoIndicativa = e.Classificacao, Destaque = e.Destaque, Status = StatusDoEvento.Publicado,
                    CriadoEmUtc = agora.AddDays(-20), AtualizadoEmUtc = agora.AddDays(-20),
                    Ingressos = e.Lotes.Select((l, i) => new TipoDeIngresso
                    {
                        Nome = l.Nome, Descricao = l.Descricao, PrecoCentavos = l.PrecoCentavos, Quantidade = l.Quantidade, Ordem = i,
                    }).ToList(),
                });
            }
            await db.SaveChangesAsync(ct);
            log.LogInformation("Semeadura: {N} eventos criados.", DadosDaDemo.Eventos.Length);
        }

        if (!await db.Pedidos.AnyAsync(ct)) await SemearVendasAsync(db, agora, ct);
        if (!await db.Visualizacoes.AnyAsync(ct)) await SemearVisualizacoesAsync(db, agora, ct);
    }

    /// <summary>
    /// Reaplica as datas relativas a "agora" (dev): a demonstração sempre tem evento ao vivo, hoje e nos próximos dias.
    /// Só mexe nos eventos de demonstração (pelo slug); os cadastrados por organizadores ficam como estão.
    /// </summary>
    public async Task AtualizarDatasAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var agora = relogio.GetUtcNow().UtcDateTime;
        var n = 0;
        foreach (var e in DadosDaDemo.Eventos)
        {
            var (ini, fim) = Datas(e, agora);
            n += await db.Eventos.Where(x => x.Slug == e.Slug)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.InicioUtc, ini).SetProperty(x => x.FimUtc, fim), ct);
        }
        log.LogInformation("Semeadura: datas de {N} eventos de demonstração atualizadas para hoje.", n);
    }

    /// <summary>
    /// As datas de um evento de demonstração. Ao vivo: começou há X minutos. Os outros: hoje+N dias à hora local; o "hoje"
    /// que já passou da hora é empurrado para daqui a pouco (a vitrine do dia nunca fica vazia).
    /// </summary>
    public static (DateTime Inicio, DateTime Fim) Datas(DadosDaDemo.EventoDemo e, DateTime agoraUtc)
    {
        DateTime ini;
        if (e.ComecouHaMin is { } m)
        {
            ini = Arredonda(agoraUtc.AddMinutes(-m));
        }
        else
        {
            var dia = BrazilTime.DataEmBrasilia(agoraUtc).AddDays(e.Dia);
            ini = BrazilTime.ToUtc(dia.ToDateTime(TimeOnly.Parse(e.Hora)));
            if (e.Dia == 0 && ini <= agoraUtc) ini = Arredonda(agoraUtc.AddMinutes(90));
        }
        return (ini, ini.AddMinutes(e.DuracaoMin));
    }

    private static DateTime Arredonda(DateTime utc)
    {
        var quinze = TimeSpan.FromMinutes(15).Ticks;
        return new DateTime(utc.Ticks / quinze * quinze, DateTimeKind.Utc);
    }

    /// <summary>Vendas pagas plausíveis (determinísticas) para o painel do organizador e do admin não nascerem zerados.</summary>
    private async Task SemearVendasAsync(MainContext db, DateTime agora, CancellationToken ct)
    {
        var publico = new List<Usuario>();
        for (var i = 1; i <= PessoasDoPublico; i++)
        {
            var email = $"publico{i:00}@umura.dev";
            var u = await db.Usuarios.FirstOrDefaultAsync(x => x.Email == email, ct);
            if (u is null)
            {
                u = new Usuario { Email = email, Nome = $"Pessoa do público {i:00}", Papel = Papel.Usuario, CriadoEmUtc = agora.AddDays(-30) };
                u.SenhaHash = hasher.HashPassword(u, Codigos.Novo(32));
                db.Usuarios.Add(u);
            }
            publico.Add(u);
        }
        await db.SaveChangesAsync(ct);

        var compradores = publico.Select(p => p.Id)
            .Concat(await db.Usuarios.Where(u => u.Email == "ana@umura.dev" || u.Email == "bruno@umura.dev").Select(u => u.Id).ToListAsync(ct))
            .ToList();
        var rnd = new Random(2026);
        var eventos = await db.Eventos.Include(e => e.Ingressos).ToListAsync(ct);
        var pedidos = 0;

        foreach (var ev in eventos)
        {
            var nPedidos = rnd.Next(3, 14);
            for (var p = 0; p < nPedidos; p++)
            {
                var tipo = ev.Ingressos[rnd.Next(ev.Ingressos.Count)];
                var qtd = Math.Min(rnd.Next(1, 4), tipo.Quantidade - tipo.Vendidos);
                if (qtd <= 0) continue;
                var criado = agora.AddDays(-rnd.Next(0, 14)).AddMinutes(-rnd.Next(0, 1440));
                var pedido = new Pedido
                {
                    Codigo = Codigos.Novo(10), UsuarioId = compradores[rnd.Next(compradores.Count)], EventoId = ev.Id,
                    Status = StatusDoPedido.Pago,
                    Metodo = tipo.PrecoCentavos == 0 ? MetodoDePagamento.Gratuito : (rnd.Next(2) == 0 ? MetodoDePagamento.Pix : MetodoDePagamento.Cartao),
                    TotalCentavos = tipo.PrecoCentavos * qtd, CriadoEmUtc = criado, ExpiraEmUtc = criado.AddMinutes(15), PagoEmUtc = criado.AddMinutes(2),
                    Itens = [new ItemDoPedido { TipoDeIngressoId = tipo.Id, NomeDoTipo = tipo.Nome, PrecoUnitarioCentavos = tipo.PrecoCentavos, Quantidade = qtd }],
                };
                if (pedido.Metodo == MetodoDePagamento.Cartao) pedido.CartaoFinal = "4242";
                for (var i = 0; i < qtd; i++)
                    pedido.IngressosEmitidos.Add(new Ingresso { EventoId = ev.Id, Codigo = Codigos.Novo(10), NomeDoTipo = tipo.Nome });
                tipo.Vendidos += qtd;
                db.Pedidos.Add(pedido);
                pedidos++;
            }
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Semeadura: {N} pedidos pagos de demonstração.", pedidos);
    }

    private async Task SemearVisualizacoesAsync(MainContext db, DateTime agora, CancellationToken ct)
    {
        var rnd = new Random(18);
        var ids = await db.Eventos.Select(e => new { e.Id, e.Destaque }).ToListAsync(ct);
        var total = 0;
        foreach (var e in ids)
        {
            var n = rnd.Next(25, e.Destaque ? 260 : 140);
            for (var i = 0; i < n; i++)
                db.Visualizacoes.Add(new VisualizacaoDeEvento { EventoId = e.Id, QuandoUtc = agora.AddDays(-rnd.Next(0, 14)).AddMinutes(-rnd.Next(0, 1440)) });
            total += n;
        }
        await db.SaveChangesAsync(ct);
        log.LogInformation("Semeadura: {N} visualizações de demonstração.", total);
    }
}
