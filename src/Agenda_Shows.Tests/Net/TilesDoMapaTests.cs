using System.Net;
using Agenda_Shows.UI.Geo;
using Agenda_Shows.UI.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Agenda_Shows.Tests.Net;

/// <summary>O proxy de tiles só serve Curitiba e região (a caixa do mapa do site) — não o Brasil inteiro até o zoom 19.</summary>
public class TilesDoMapaTests
{
    // Lat/lng → tile, a conta padrão do OSM (Web Mercator).
    private static (int X, int Y) Tile(double lat, double lng, int z)
    {
        var n = 1 << z;
        var x = (int)Math.Floor((lng + 180) / 360 * n);
        var r = lat * Math.PI / 180;
        var y = (int)Math.Floor((1 - Math.Log(Math.Tan(r) + 1 / Math.Cos(r)) / Math.PI) / 2 * n);
        return (x, y);
    }

    [Theory]
    [InlineData(-25.4284, -49.2733, 13)] // Praça Tiradentes
    [InlineData(-25.4284, -49.2733, 19)]
    [InlineData(-25.3846, -49.2768, 17)] // Ópera de Arame
    [InlineData(-25.5300, -49.1750, 15)] // São José dos Pinhais (região)
    [InlineData(-25.4284, -49.2733, 11)] // o zoom mais aberto do mapa
    public void Curitiba_EhValido(double lat, double lng, int z)
    {
        var (x, y) = Tile(lat, lng, z);
        Assert.True(TilesDoMapa.Valido(z, x, y));
    }

    [Theory]
    [InlineData(-23.5505, -46.6333, 13)] // São Paulo
    [InlineData(-22.9068, -43.1729, 19)] // Rio de Janeiro, no zoom de raspagem
    [InlineData(-30.0346, -51.2177, 15)] // Porto Alegre
    [InlineData(48.8566, 2.3522, 10)]    // Paris
    public void ForaDaRegiao_NaoEhValido(double lat, double lng, int z)
    {
        var (x, y) = Tile(lat, lng, z);
        Assert.False(TilesDoMapa.Valido(z, x, y));
    }

    [Theory]
    [InlineData(20, 0, 0)]
    [InlineData(2, 0, 0)]
    [InlineData(13, -1, 5)]
    [InlineData(13, 5, 9000)]
    public void ZoomOuCoordenadaImpossivel_NaoEhValido(int z, int x, int y) => Assert.False(TilesDoMapa.Valido(z, x, y));

    private static HttpContext De(string ip)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        return http;
    }

    [Fact]
    public void BuscasNovas_TemLimitePorIp()
    {
        using var limite = new LimiteDeBuscasDeTiles(new ClientIpResolver(new TrustedProxyOptions()),
            Options.Create(new OpcoesDosTiles { BuscasNovasPorMinutoPorIp = 3 }));
        var raspador = De("203.0.113.7");
        Assert.True(limite.Permitir(raspador));
        Assert.True(limite.Permitir(raspador));
        Assert.True(limite.Permitir(raspador));
        Assert.False(limite.Permitir(raspador));          // estourou o minuto deste IP
        Assert.True(limite.Permitir(De("198.51.100.9")));  // outra pessoa segue normal
    }
}
