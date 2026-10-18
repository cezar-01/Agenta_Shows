using System.Globalization;
using System.Text.Json;
using Agenda_Shows.Entities;
using Agenda_Shows.Services.Comum;
using Agenda_Shows.Services.Eventos;

namespace Agenda_Shows.UI.Seo;

/// <summary>
/// O bloco <c>application/ld+json</c> schema.org/Event da página do evento (é o que faz o Google mostrar o evento com data
/// e local na busca). Molde do JsonLd do MyPods: é um bloco de DADOS, não um script — a CSP (script-src sem inline) não o
/// alcança, porque ele nunca executa. O JSON sai do serializador padrão, que escapa "&lt;" e "&gt;": um "&lt;/script&gt;"
/// digitado no título não fecha o bloco.
/// </summary>
public static class JsonLdEvento
{
    private static readonly JsonSerializerOptions Opcoes = new() { WriteIndented = false };

    public static string Bloco(EventoDetalhe d, string baseUri)
    {
        var e = d.Card;
        var url = baseUri.TrimEnd('/') + "/evento/" + e.Slug;
        var ofertas = d.Lotes.Select(l => new Dictionary<string, object?>
        {
            ["@type"] = "Offer",
            ["name"] = l.Nome,
            ["price"] = (l.PrecoCentavos / 100m).ToString("0.00", CultureInfo.InvariantCulture),
            ["priceCurrency"] = "BRL",
            ["availability"] = l.Esgotado ? "https://schema.org/SoldOut" : "https://schema.org/InStock",
            ["url"] = url,
        }).ToList();

        var evento = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Event",
            ["name"] = e.Titulo,
            ["description"] = e.Resumo,
            ["startDate"] = e.InicioUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["endDate"] = e.FimUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ["eventStatus"] = e.Status == StatusDoEvento.Cancelado ? "https://schema.org/EventCancelled" : "https://schema.org/EventScheduled",
            ["eventAttendanceMode"] = "https://schema.org/OfflineEventAttendanceMode",
            ["image"] = new[] { baseUri.TrimEnd('/') + Foto.Cheia(e.ImagemUrl) },
            ["url"] = url,
            ["location"] = new Dictionary<string, object?>
            {
                ["@type"] = "Place",
                ["name"] = e.LocalNome,
                ["address"] = new Dictionary<string, object?>
                {
                    ["@type"] = "PostalAddress",
                    ["streetAddress"] = d.Endereco,
                    ["addressLocality"] = "Curitiba",
                    ["addressRegion"] = "PR",
                    ["addressCountry"] = "BR",
                },
                ["geo"] = new Dictionary<string, object?>
                {
                    ["@type"] = "GeoCoordinates",
                    ["latitude"] = e.Latitude,
                    ["longitude"] = e.Longitude,
                },
            },
            ["organizer"] = new Dictionary<string, object?> { ["@type"] = "Organization", ["name"] = e.Organizador },
            ["offers"] = ofertas,
        };
        return "<script type=\"application/ld+json\">" + JsonSerializer.Serialize(evento, Opcoes) + "</script>";
    }
}
