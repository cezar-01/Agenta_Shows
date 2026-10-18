using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Agenda_Shows.Services.Comum;

/// <summary>Códigos aleatórios e slugs.</summary>
public static class Codigos
{
    // Sem 0/O, 1/I/L: o código do ingresso é lido em voz alta na porta do evento.
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>Código criptograficamente aleatório — a URL do pedido não é sequencial nem adivinhável.</summary>
    public static string Novo(int tamanho)
    {
        Span<char> c = stackalloc char[tamanho];
        for (var i = 0; i < tamanho; i++) c[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return new string(c);
    }

    /// <summary>"Noite de Pop Rock na Ópera!" → "noite-de-pop-rock-na-opera".</summary>
    public static string Slug(string texto, int maximo = 90)
    {
        var semAcento = new StringBuilder();
        foreach (var ch in (texto ?? string.Empty).Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) semAcento.Append(ch);

        var sb = new StringBuilder();
        var hifen = false;
        foreach (var ch in semAcento.ToString().ToLowerInvariant())
        {
            if (ch is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(ch); hifen = false; }
            else if (!hifen && sb.Length > 0) { sb.Append('-'); hifen = true; }
            if (sb.Length >= maximo) break;
        }
        var s = sb.ToString().Trim('-');
        return s.Length == 0 ? "evento" : s;
    }
}

/// <summary>A foto do evento: o banco guarda a base ("/img/eventos/shows-1"); a página escolhe o tamanho.</summary>
public static class Foto
{
    public const string Padrao = "/img/eventos/shows-1";

    public static string Card(string? baseUrl) => (string.IsNullOrWhiteSpace(baseUrl) ? Padrao : baseUrl) + "-card.webp";

    public static string Cheia(string? baseUrl) => (string.IsNullOrWhiteSpace(baseUrl) ? Padrao : baseUrl) + ".webp";
}
