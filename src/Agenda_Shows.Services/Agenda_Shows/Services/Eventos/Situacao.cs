using Agenda_Shows.Entities;
using Agenda_Shows.Services.Time;

namespace Agenda_Shows.Services.Eventos;

/// <summary>Em que pé o evento está agora — o que a vitrine e o nicho usam para separar "ao vivo", "hoje" e "em breve".</summary>
public enum SituacaoDoEvento
{
    AoVivo,
    Hoje,
    EmBreve,
    Encerrado,
    Cancelado,
}

/// <summary>A régua única de "está rolando agora?". Hoje é o dia de Brasília (BrazilTime), nunca o dia UTC.</summary>
public static class Situacao
{
    public static SituacaoDoEvento De(DateTime inicioUtc, DateTime fimUtc, StatusDoEvento status, DateTime agoraUtc)
    {
        if (status == StatusDoEvento.Cancelado) return SituacaoDoEvento.Cancelado;
        if (agoraUtc >= fimUtc) return SituacaoDoEvento.Encerrado;
        if (agoraUtc >= inicioUtc) return SituacaoDoEvento.AoVivo;
        return BrazilTime.DataEmBrasilia(inicioUtc) == BrazilTime.DataEmBrasilia(agoraUtc)
            ? SituacaoDoEvento.Hoje
            : SituacaoDoEvento.EmBreve;
    }

    /// <summary>
    /// O texto do selo: "Ao vivo agora", "Começa em 40 min", "Hoje · 20:30", "Sáb · 10:00", "Encerrado", "Cancelado".
    /// "Começa em" só na última hora antes de começar — antes disso, a hora diz mais que a contagem.
    /// </summary>
    public static string Rotulo(DateTime inicioUtc, DateTime fimUtc, StatusDoEvento status, DateTime agoraUtc)
    {
        var s = De(inicioUtc, fimUtc, status, agoraUtc);
        return s switch
        {
            SituacaoDoEvento.Cancelado => "Cancelado",
            SituacaoDoEvento.Encerrado => "Encerrado",
            SituacaoDoEvento.AoVivo => "Ao vivo agora",
            SituacaoDoEvento.Hoje when inicioUtc - agoraUtc <= TimeSpan.FromMinutes(60) =>
                $"Começa em {Math.Max(1, (int)Math.Ceiling((inicioUtc - agoraUtc).TotalMinutes))} min",
            _ => QuandoDoEvento.Rotulo(inicioUtc, agoraUtc),
        };
    }

    /// <summary>"até 23:00" para quem está ao vivo — quanto ainda dá tempo.</summary>
    public static string AteQuando(DateTime fimUtc, DateTime agoraUtc) =>
        BrazilTime.DiasDeCalendario(agoraUtc, fimUtc) == 0
            ? $"até {BrazilTime.Format(fimUtc, "HH:mm")}"
            : $"até {BrazilTime.Format(fimUtc, "ddd HH:mm")}";
}

public static class Dinheiro
{
    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>"R$ 60,00". Centavos → texto; dinheiro nunca passa por double.</summary>
    public static string Reais(int centavos) => (centavos / 100m).ToString("C", PtBr);

    /// <summary>"Grátis", "Tem opção grátis", "R$ 25,00" ou "a partir de R$ 25,00".</summary>
    public static string APartirDe(int? menorCentavos, bool variosPrecos)
    {
        if (menorCentavos is null or 0) return variosPrecos ? "Tem opção grátis" : "Grátis";
        return variosPrecos ? $"a partir de {Reais(menorCentavos.Value)}" : Reais(menorCentavos.Value);
    }
}
