using System.Globalization;

namespace Agenda_Shows.Services.Time;

/// <summary>
/// A régua ÚNICA do fuso de Brasília (copiada do MyPods, e morando no Services de propósito: lá havia uma segunda
/// cópia no projeto de serviço, e duas réguas divergem). O banco guarda UTC; tudo que o público lê — "hoje",
/// "amanhã", "fim de semana", a hora do evento — sai daqui.
/// </summary>
/// <remarks>
/// ⚠️ O processo roda em UTC em produção (nem o Dockerfile fixa fuso). Entre as 21h e a meia-noite de Brasília o dia
/// UTC já é o seguinte: um filtro "hoje" com <c>DateTime.UtcNow.Date</c> mostra os eventos de AMANHÃ justamente na
/// hora em que mais gente procura o que fazer. Use <see cref="DataEmBrasilia"/> e <see cref="InicioDoDiaUtc"/>.
/// </remarks>
public static class BrazilTime
{
    private static readonly TimeZoneInfo Tz = ResolveTz();
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static TimeZoneInfo ResolveTz()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.CreateCustomTimeZone("BRT-fallback", TimeSpan.FromHours(-3), "Horário de Brasília", "BRT");
    }

    public static DateTime ToBrasilia(DateTime dt)
    {
        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Tz);
    }

    /// <summary>Hora local de Brasília (a que o produtor digita) → UTC, para gravar.</summary>
    public static DateTime ToUtc(DateTime local)
    {
        var unspec = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        try { return TimeZoneInfo.ConvertTimeToUtc(unspec, Tz); }
        catch (ArgumentException) { return DateTime.SpecifyKind(unspec - Tz.BaseUtcOffset, DateTimeKind.Utc); }
    }

    /// <summary>O dia do calendário de BRASÍLIA em que o instante cai — nunca o dia UTC.</summary>
    public static DateOnly DataEmBrasilia(DateTime dt) => DateOnly.FromDateTime(ToBrasilia(dt));

    /// <summary>A meia-noite de Brasília daquele dia, em UTC: o começo da janela [início, fim) de um filtro por dia.</summary>
    public static DateTime InicioDoDiaUtc(DateOnly dia) => ToUtc(dia.ToDateTime(TimeOnly.MinValue));

    /// <summary>
    /// Quantos dias do calendário de Brasília vão de <paramref name="de"/> até <paramref name="ate"/>. Conta DIAS,
    /// nunca horas/24: ontem às 23h visto hoje às 10h são 11 h e UM dia.
    /// </summary>
    public static int DiasDeCalendario(DateTime de, DateTime ate)
        => DataEmBrasilia(ate).DayNumber - DataEmBrasilia(de).DayNumber;

    /// <summary>O valor de um <c>&lt;input type="datetime-local"&gt;</c>, na hora de Brasília.</summary>
    public static string ParaInputLocal(DateTime utc)
        => ToBrasilia(utc).ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    public static string Format(DateTime dt, string format) => ToBrasilia(dt).ToString(format, PtBr);
}
