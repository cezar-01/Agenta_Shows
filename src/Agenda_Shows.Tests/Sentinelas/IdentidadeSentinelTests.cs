using System.Text.RegularExpressions;
using Agenda_Shows.UI.Net;

namespace Agenda_Shows.Tests.Sentinelas;

/// <summary>
/// Sentinelas de FONTE: leem o texto dos arquivos, porque o que elas protegem (CSS, fonte, CSP) nenhum teste de
/// componente executa. Cada uma nasceu de um defeito real do MyPods.
/// </summary>
public partial class IdentidadeSentinelTests
{
    private static readonly string Ui = AcharUi();

    private static string AcharUi()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Agenda_Shows.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Agenda_Shows.UI");
    }

    private static IEnumerable<string> Arquivos(string padrao) =>
        Directory.EnumerateFiles(Ui, padrao, SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                     && !f.Contains(Path.DirectorySeparatorChar + "vendor" + Path.DirectorySeparatorChar));

    [Fact]
    public void NenhumaFonteSaiPraTerceiro_ETodoArquivoCitadoEhWoff2()
    {
        foreach (var f in Arquivos("*.razor").Concat(Arquivos("*.css")))
            Assert.DoesNotMatch(@"fonts\.g(oogleapis|static)", File.ReadAllText(f));

        var css = File.ReadAllText(Path.Combine(Ui, "wwwroot", "app.css"));
        var citados = UrlDeFonte().Matches(css).Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.NotEmpty(citados);
        foreach (var url in citados)
        {
            var caminho = Path.Combine(Ui, "wwwroot", url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(caminho), $"fonte citada e ausente: {url}");
            var cabeca = new byte[4];
            using (var s = File.OpenRead(caminho)) s.ReadExactly(cabeca);
            Assert.Equal("wOF2", System.Text.Encoding.ASCII.GetString(cabeca));
        }
    }

    /// <summary>
    /// Cor literal em página é identidade re-digitada: trocar a cor da marca no app.css não alcançaria a tela. A única
    /// exceção é a meta theme-color do App.razor (o navegador não lê var() ali).
    /// </summary>
    [Fact]
    public void PaginaNaoEscreveCorLiteral()
    {
        foreach (var f in Arquivos("*.razor"))
        {
            if (Path.GetFileName(f) == "App.razor") continue;
            var texto = File.ReadAllText(f);
            var achados = CorLiteral().Matches(texto).Select(m => m.Value).ToList();
            Assert.True(achados.Count == 0, $"{Path.GetFileName(f)}: {string.Join(", ", achados)}");
        }
    }

    [Fact]
    public void TodoVarUsadoNoAppCss_EstaDeclarado()
    {
        var css = File.ReadAllText(Path.Combine(Ui, "wwwroot", "app.css"));
        var declarados = Declaracao().Matches(css).Select(m => m.Groups[1].Value).ToHashSet();
        var usados = Uso().Matches(css).Select(m => m.Groups[1].Value).Distinct();
        foreach (var u in usados) Assert.Contains(u, declarados);
    }

    [Fact]
    public void Csp_NaoAceitaScriptInline_ETudoVemDaPropriaOrigem()
    {
        foreach (var dev in new[] { false, true })
        {
            var csp = Seguranca.Csp(dev);
            var script = csp.Split(';').Select(d => d.Trim()).Single(d => d.StartsWith("script-src"));
            Assert.DoesNotContain("unsafe-inline", script);
            Assert.Contains("img-src 'self' data:", csp);
            Assert.Contains("font-src 'self'", csp);
        }
        Assert.Contains("connect-src 'self'", Seguranca.Csp(false));
        Assert.DoesNotContain("localhost", Seguranca.Csp(false));
    }

    [GeneratedRegex(@"url\('(/fonts/[^']+)'\)")]
    private static partial Regex UrlDeFonte();

    [GeneratedRegex(@"#[0-9a-fA-F]{3,8}\b(?![\w-])|rgba?\(")]
    private static partial Regex CorLiteral();

    [GeneratedRegex(@"(--um-[a-z0-9-]+)\s*:")]
    private static partial Regex Declaracao();

    [GeneratedRegex(@"var\((--um-[a-z0-9-]+)")]
    private static partial Regex Uso();
}
