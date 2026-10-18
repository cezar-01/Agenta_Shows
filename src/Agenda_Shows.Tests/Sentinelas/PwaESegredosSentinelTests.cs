using System.Text.Json;
using System.Text.RegularExpressions;

namespace Agenda_Shows.Tests.Sentinelas;

/// <summary>O PWA instalável e nenhuma credencial no repositório.</summary>
public partial class PwaESegredosSentinelTests
{
    private static readonly string Raiz = AcharRaiz();
    private static string Ui => Path.Combine(Raiz, "src", "Agenda_Shows.UI");
    private static string Www => Path.Combine(Ui, "wwwroot");

    private static string AcharRaiz()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "src", "Agenda_Shows.slnx"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void Manifesto_TemOQueOChromeExigeParaInstalar_ETodoIconeExiste()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(Www, "manifest.webmanifest")));
        var m = doc.RootElement;
        foreach (var chave in new[] { "name", "short_name", "start_url", "display", "id", "icons" })
            Assert.True(m.TryGetProperty(chave, out _), $"manifesto sem {chave}");
        Assert.Equal("standalone", m.GetProperty("display").GetString());

        var tamanhos = new HashSet<string>();
        foreach (var icone in m.GetProperty("icons").EnumerateArray())
        {
            var src = icone.GetProperty("src").GetString()!;
            Assert.True(File.Exists(Path.Combine(Www, src.TrimStart('/'))), $"ícone ausente: {src}");
            tamanhos.Add(icone.GetProperty("sizes").GetString()!);
        }
        Assert.Contains("192x192", tamanhos);
        Assert.Contains("512x512", tamanhos);
    }

    /// <summary>
    /// O precache só busca da rede no install: arquivo precacheado que sumir quebra a instalação do SW, e o nome do
    /// cache precisa mudar quando o conteúdo muda (a regra está no topo do service-worker.js).
    /// </summary>
    [Fact]
    public void ServiceWorker_PrecacheExiste_ENaoInterceptaOCircuitoNemAApi()
    {
        var sw = File.ReadAllText(Path.Combine(Www, "service-worker.js"));
        Assert.Matches(@"const CACHE = 'umura-v\d+'", sw);
        var lista = Regex.Match(sw, @"const PRECACHE = \[(.*?)\];", RegexOptions.Singleline).Groups[1].Value;
        foreach (Match arq in Regex.Matches(lista, @"'([^']+)'"))
            Assert.True(File.Exists(Path.Combine(Www, arq.Groups[1].Value.TrimStart('/'))), $"precache aponta para arquivo ausente: {arq.Groups[1].Value}");

        foreach (var prefixo in new[] { "/_blazor", "/_framework", "/api" })
            Assert.Contains($"startsWith('{prefixo}')", sw);
    }

    /// <summary>Nenhuma senha ou string de conexão com senha versionada (pedido do Gabriel: tudo nos user-secrets / .env).</summary>
    [Fact]
    public void NenhumaCredencialVersionada()
    {
        var arquivos = new[]
        {
            Path.Combine(Ui, "appsettings.json"),
            Path.Combine(Ui, "appsettings.Development.json"),
            Path.Combine(Ui, "Properties", "launchSettings.json"),
            Path.Combine(Raiz, ".claude", "launch.json"),
            Path.Combine(Raiz, "docker", "mysql", "docker-compose.yml"),
        }.Where(File.Exists);

        foreach (var f in arquivos)
        {
            var texto = File.ReadAllText(f);
            Assert.DoesNotMatch(SenhaNaConexao(), texto);
            Assert.DoesNotMatch(SenhaLiteral(), texto);
        }

        var codigo = Directory.EnumerateFiles(Path.Combine(Raiz, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains("Agenda_Shows.Tests"));
        foreach (var f in codigo)
            Assert.DoesNotMatch(SenhaNaConexao(), File.ReadAllText(f));
    }

    [Fact]
    public void ArquivoDeSenhasDoDocker_FicaForaDoGit()
    {
        var gitignore = File.ReadAllText(Path.Combine(Raiz, ".gitignore"));
        Assert.Matches(@"(?m)^\.env\s*$", gitignore);
        Assert.True(File.Exists(Path.Combine(Raiz, "docker", "mysql", ".env.example")));
    }

    [GeneratedRegex(@"(?i)(password|pwd)\s*=\s*[^;""'\s<>{}]+")]
    private static partial Regex SenhaNaConexao();

    [GeneratedRegex(@"(?i)""(password|senha|MYSQL_ROOT_PASSWORD)""\s*:\s*""[^""]+""")]
    private static partial Regex SenhaLiteral();
}
