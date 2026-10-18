using System.Net;
using Agenda_Shows.UI.Net;
using Microsoft.AspNetCore.Http;

namespace Agenda_Shows.Tests.Net;

public class ClientIpResolverTests
{
    private static HttpContext Requisicao(string conexao, string? xff = null)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(conexao);
        if (xff is not null) http.Request.Headers["X-Forwarded-For"] = xff;
        return http;
    }

    [Fact]
    public void SemProxyDeclarado_OCabecalhoEhIgnorado()
    {
        var r = new ClientIpResolver(new TrustedProxyOptions());
        Assert.Equal("10.0.0.5", r.PartitionKey(Requisicao("10.0.0.5", "1.2.3.4")));
    }

    [Fact]
    public void ProxyDeclarado_LeOSaltoDaDireita()
    {
        var r = new ClientIpResolver(new TrustedProxyOptions { TrustedProxyHops = 1, KnownNetworks = ["10.0.0.0/8"] });

        // O cliente inventou "6.6.6.6" à esquerda; o proxy acrescentou o IP real à direita.
        Assert.Equal("200.1.2.3", r.PartitionKey(Requisicao("10.0.0.5", "6.6.6.6, 200.1.2.3")));
    }

    [Fact]
    public void ConexaoDeForaDaRedeDoProxy_NaoFalaPeloCabecalho()
    {
        var r = new ClientIpResolver(new TrustedProxyOptions { TrustedProxyHops = 1, KnownNetworks = ["10.0.0.0/8"] });
        Assert.Equal("8.8.8.8", r.PartitionKey(Requisicao("8.8.8.8", "200.1.2.3")));
    }

    [Fact]
    public void IPv6_AgrupaPor64()
    {
        var r = new ClientIpResolver(new TrustedProxyOptions());
        Assert.Equal("2804:14c::/64", r.PartitionKey(Requisicao("2804:14c:0:0:aaaa:bbbb:cccc:dddd")));
    }

    [Theory]
    [InlineData("GET", "/eventos", null, true)]
    [InlineData("POST", "/eventos", null, false)]
    [InlineData("GET", "/api/mapa/tiles/1/2/3.png", "image/png", false)]
    [InlineData("GET", "/api/coisa", "text/html,application/xhtml+xml", true)]
    public void PaginaDeStatus_SoReexecutaGetForaDaApi(string metodo, string caminho, string? accept, bool esperado)
        => Assert.Equal(esperado, StatusCodePageDecisions.DeveReexecutar(metodo, caminho, accept));
}
