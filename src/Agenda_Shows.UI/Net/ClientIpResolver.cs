using System.Net;
using System.Net.Sockets;

namespace Agenda_Shows.UI.Net;

/// <summary>Os proxies em que o app confia (seção <c>Network</c> da configuração).</summary>
public sealed class TrustedProxyOptions
{
    public string[] KnownProxies { get; set; } = Array.Empty<string>();

    public string[] KnownNetworks { get; set; } = Array.Empty<string>();

    /// <summary>Quantos proxies acrescentam o IP observado ao <c>X-Forwarded-For</c> (Railway/Traefik = 1).</summary>
    public int TrustedProxyHops { get; set; }
}

/// <summary>
/// A chave do rate limiter por cliente (copiado do MyPods). O <c>X-Forwarded-For</c> só é lido quando a conexão
/// vem de um proxy DECLARADO e contando os saltos da direita — ler o cabeçalho cru deixaria qualquer um escolher a
/// própria partição. Sem proxy configurado atrás de um, todo visitante cai no mesmo balde (o boot avisa).
/// IPv6 agrupa por /64, que é o que um aparelho costuma receber.
/// </summary>
public sealed class ClientIpResolver
{
    private readonly List<IPAddress> proxies = new();
    private readonly List<(IPAddress Prefix, int Bits)> networks = new();
    private readonly int hops;

    public ClientIpResolver(TrustedProxyOptions options)
    {
        hops = Math.Max(0, options.TrustedProxyHops);

        foreach (var raw in options.KnownProxies ?? Array.Empty<string>())
            if (IPAddress.TryParse(raw?.Trim(), out var ip))
                proxies.Add(Normalizar(ip));

        foreach (var raw in options.KnownNetworks ?? Array.Empty<string>())
        {
            var parts = (raw ?? string.Empty).Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length == 2 &&
                IPAddress.TryParse(parts[0], out var prefix) &&
                int.TryParse(parts[1], out var bits) &&
                bits >= 0)
            {
                networks.Add((Normalizar(prefix), bits));
            }
        }
    }

    public bool ConfiaEmProxy => hops > 0 && (proxies.Count > 0 || networks.Count > 0);

    public string PartitionKey(HttpContext context)
    {
        var conexao = context.Connection.RemoteIpAddress;
        if (conexao is null) return "anon";
        conexao = Normalizar(conexao);

        if (ConfiaEmProxy && EhProxyConfiavel(conexao) &&
            TentarLerEncaminhado(context, out var real))
        {
            return Agrupar(real);
        }

        return Agrupar(conexao);
    }

    private bool TentarLerEncaminhado(HttpContext context, out IPAddress resultado)
    {
        resultado = IPAddress.None;

        var xff = context.Request.Headers["X-Forwarded-For"].ToString();
        if (string.IsNullOrWhiteSpace(xff)) return false;

        var lista = xff.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Conta da direita: o último salto é o do proxy confiável; os da esquerda o cliente pode inventar.
        var indice = lista.Length - hops;
        if (indice < 0 || indice >= lista.Length) return false;

        var candidato = lista[indice];
        if (candidato.StartsWith('[') && candidato.Contains(']'))
            candidato = candidato[1..candidato.IndexOf(']')];

        if (!IPAddress.TryParse(candidato, out var ip)) return false;

        resultado = Normalizar(ip);
        return true;
    }

    private bool EhProxyConfiavel(IPAddress ip)
    {
        foreach (var p in proxies)
            if (p.Equals(ip)) return true;

        foreach (var (prefix, bits) in networks)
            if (DentroDaRede(ip, prefix, bits)) return true;

        return false;
    }

    private static bool DentroDaRede(IPAddress ip, IPAddress prefix, int bits)
    {
        if (ip.AddressFamily != prefix.AddressFamily) return false;

        var a = ip.GetAddressBytes();
        var b = prefix.GetAddressBytes();
        if (bits > a.Length * 8) return false;

        var bytesInteiros = bits / 8;
        for (var i = 0; i < bytesInteiros; i++)
            if (a[i] != b[i]) return false;

        var sobra = bits % 8;
        if (sobra == 0) return true;

        var mascara = (byte)(0xFF << (8 - sobra));
        return (a[bytesInteiros] & mascara) == (b[bytesInteiros] & mascara);
    }

    private static IPAddress Normalizar(IPAddress ip) =>
        ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;

    private static string Agrupar(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetworkV6) return ip.ToString();

        var bytes = ip.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
