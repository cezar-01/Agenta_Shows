using System.ComponentModel.DataAnnotations;

namespace Agenda_Shows.Entities;

/// <summary>Os três tipos de conta. O papel vai na claim do cookie e é conferido de novo no serviço.</summary>
public enum Papel
{
    /// <summary>Quem descobre eventos, favorita e compra ingresso.</summary>
    Usuario = 0,

    /// <summary>Quem cadastra eventos — e só enxerga os próprios.</summary>
    Organizador = 1,

    /// <summary>A equipe da UMURA: enxerga e modera tudo.</summary>
    Admin = 2,
}

public class Usuario
{
    public int Id { get; set; }

    [MaxLength(120)] public string Nome { get; set; } = string.Empty;

    /// <summary>Sempre em minúsculas (o serviço normaliza) — é a chave do login.</summary>
    [MaxLength(200)] public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 do <c>PasswordHasher</c>. A senha em claro nunca chega ao banco.</summary>
    [MaxLength(400)] public string SenhaHash { get; set; } = string.Empty;

    public Papel Papel { get; set; } = Papel.Usuario;

    /// <summary>Nome público do organizador (o coletivo, a produtora). Só faz sentido para quem organiza.</summary>
    [MaxLength(120)] public string? NomeDoOrganizador { get; set; }

    public DateTime CriadoEmUtc { get; set; }

    public List<Evento> Eventos { get; set; } = [];
}

/// <summary>O "nicho" da vitrine (o equivalente às categorias do iFood): shows, festas, teatro...</summary>
public class Categoria
{
    public int Id { get; set; }

    [MaxLength(40)] public string Slug { get; set; } = string.Empty;

    [MaxLength(60)] public string Nome { get; set; } = string.Empty;

    [MaxLength(200)] public string Descricao { get; set; } = string.Empty;

    /// <summary>A capa redonda do nicho, servida do wwwroot (as imagens não moram no banco).</summary>
    [MaxLength(200)] public string ImagemUrl { get; set; } = string.Empty;

    public int Ordem { get; set; }

    public List<Evento> Eventos { get; set; } = [];
}

public enum StatusDoEvento
{
    Rascunho = 0,
    Publicado = 1,
    Cancelado = 2,
}

public class Evento
{
    public int Id { get; set; }

    [MaxLength(140)] public string Slug { get; set; } = string.Empty;

    [MaxLength(120)] public string Titulo { get; set; } = string.Empty;

    /// <summary>Uma linha, a que aparece no card.</summary>
    [MaxLength(200)] public string Resumo { get; set; } = string.Empty;

    [MaxLength(4000)] public string Descricao { get; set; } = string.Empty;

    public int CategoriaId { get; set; }
    public Categoria? Categoria { get; set; }

    /// <summary>Quem cadastrou. O organizador só lê e mexe nos eventos em que é este id.</summary>
    public int OrganizadorId { get; set; }
    public Usuario? Organizador { get; set; }

    [MaxLength(120)] public string LocalNome { get; set; } = string.Empty;

    [MaxLength(200)] public string Endereco { get; set; } = string.Empty;

    [MaxLength(80)] public string Bairro { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>Sempre UTC no banco. "Hoje" e "ao vivo" são decididos pelo relógio de Brasília (BrazilTime).</summary>
    public DateTime InicioUtc { get; set; }

    public DateTime FimUtc { get; set; }

    /// <summary>Caminho da foto em <c>wwwroot/img/eventos</c> (sem extensão de tamanho: a página escolhe card ou cheia).</summary>
    [MaxLength(200)] public string ImagemUrl { get; set; } = string.Empty;

    /// <summary>Idade mínima (0 = livre).</summary>
    public int ClassificacaoIndicativa { get; set; }

    /// <summary>Destaque da vitrine — decisão do admin.</summary>
    public bool Destaque { get; set; }

    public StatusDoEvento Status { get; set; } = StatusDoEvento.Publicado;

    public DateTime CriadoEmUtc { get; set; }

    public DateTime AtualizadoEmUtc { get; set; }

    public List<TipoDeIngresso> Ingressos { get; set; } = [];
}

/// <summary>Um lote de ingresso do evento (Inteira, Meia, VIP, Gratuito...). Preço em centavos: dinheiro não é double.</summary>
public class TipoDeIngresso
{
    public int Id { get; set; }

    public int EventoId { get; set; }
    public Evento? Evento { get; set; }

    [MaxLength(60)] public string Nome { get; set; } = string.Empty;

    [MaxLength(160)] public string? Descricao { get; set; }

    public int PrecoCentavos { get; set; }

    public int Quantidade { get; set; }

    /// <summary>
    /// Reservados + pagos. Sobe na criação do pedido por UPDATE condicional (<c>Vendidos + n &lt;= Quantidade</c>) — duas
    /// pessoas comprando o último ingresso ao mesmo tempo não passam as duas — e volta quando o pedido expira ou é cancelado.
    /// </summary>
    public int Vendidos { get; set; }

    public int Ordem { get; set; }
}

public enum StatusDoPedido
{
    AguardandoPagamento = 0,
    Pago = 1,
    Cancelado = 2,
    Expirado = 3,
}

public enum MetodoDePagamento
{
    Nenhum = 0,
    Pix = 1,
    Cartao = 2,
    Gratuito = 3,
}

/// <summary>
/// O pedido de ingressos. O pagamento é SIMULADO (ambiente de demonstração): nenhum dado de cartão é guardado além dos
/// quatro últimos dígitos do cartão de teste, e nenhum valor é cobrado.
/// </summary>
public class Pedido
{
    public int Id { get; set; }

    /// <summary>O código que a pessoa vê e que vai na URL (<c>/pedido/{codigo}</c>). Aleatório: a URL não é sequencial.</summary>
    [MaxLength(12)] public string Codigo { get; set; } = string.Empty;

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public int EventoId { get; set; }
    public Evento? Evento { get; set; }

    public StatusDoPedido Status { get; set; }

    public MetodoDePagamento Metodo { get; set; }

    public int TotalCentavos { get; set; }

    public DateTime CriadoEmUtc { get; set; }

    /// <summary>Até quando a reserva segura os ingressos. Passou sem pagar, o job expira e devolve ao lote.</summary>
    public DateTime ExpiraEmUtc { get; set; }

    public DateTime? PagoEmUtc { get; set; }

    [MaxLength(4)] public string? CartaoFinal { get; set; }

    public List<ItemDoPedido> Itens { get; set; } = [];

    public List<Ingresso> IngressosEmitidos { get; set; } = [];
}

public class ItemDoPedido
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int TipoDeIngressoId { get; set; }

    /// <summary>Retrato do nome e do preço na hora da compra: mudar o lote depois não reescreve o pedido.</summary>
    [MaxLength(60)] public string NomeDoTipo { get; set; } = string.Empty;

    public int PrecoUnitarioCentavos { get; set; }

    public int Quantidade { get; set; }
}

/// <summary>Um ingresso emitido (um por pessoa), com o código que vira QR e que o organizador valida na porta.</summary>
public class Ingresso
{
    public int Id { get; set; }

    public int PedidoId { get; set; }
    public Pedido? Pedido { get; set; }

    public int EventoId { get; set; }

    [MaxLength(16)] public string Codigo { get; set; } = string.Empty;

    [MaxLength(60)] public string NomeDoTipo { get; set; } = string.Empty;

    public DateTime? UsadoEmUtc { get; set; }
}

public class Favorito
{
    public int UsuarioId { get; set; }

    public int EventoId { get; set; }
    public Evento? Evento { get; set; }

    public DateTime CriadoEmUtc { get; set; }
}

/// <summary>
/// Uma visualização da página do evento — anônima por construção (molde do PageView do MyPods): sem usuário, sem IP,
/// sem user-agent. É o número do dashboard do organizador.
/// </summary>
public class VisualizacaoDeEvento
{
    public long Id { get; set; }

    public int EventoId { get; set; }

    public DateTime QuandoUtc { get; set; }
}
