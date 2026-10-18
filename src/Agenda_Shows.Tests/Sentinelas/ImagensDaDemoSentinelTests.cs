using Agenda_Shows.Services.Demo;

namespace Agenda_Shows.Tests.Sentinelas;

/// <summary>
/// As ilustrações da demonstração moram no wwwroot (geradas em design/ilustracoes), não no banco: o seed só guarda o nome.
/// Regerar e esquecer um arquivo vira card cinza em produção sem erro nenhum — este teste pega antes.
/// </summary>
public class ImagensDaDemoSentinelTests
{
    private static readonly string Img = AcharImg();

    private static string AcharImg()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Agenda_Shows.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "Agenda_Shows.UI", "wwwroot", "img");
    }

    private static void AssertWebp(string caminho)
    {
        Assert.True(File.Exists(caminho), $"imagem ausente: {caminho}");
        var cabecalho = new byte[12];
        using (var f = File.OpenRead(caminho)) f.ReadExactly(cabecalho);
        // RIFF....WEBP: a extensão diz webp, o conteúdo também precisa dizer.
        Assert.True(cabecalho[..4].SequenceEqual("RIFF"u8.ToArray()) && cabecalho[8..12].SequenceEqual("WEBP"u8.ToArray()), $"não é WebP: {caminho}");
    }

    [Fact]
    public void TodoEventoDaDemo_TemAImagemCheiaEOCard()
    {
        foreach (var e in DadosDaDemo.Eventos)
        {
            AssertWebp(Path.Combine(Img, "eventos", e.Foto + ".webp"));
            AssertWebp(Path.Combine(Img, "eventos", e.Foto + "-card.webp"));
        }
    }

    [Fact]
    public void TodoNicho_TemOAvatar()
    {
        foreach (var n in DadosDaDemo.Nichos)
            AssertWebp(Path.Combine(Img, "nichos", n.Slug + ".webp"));
    }
}
