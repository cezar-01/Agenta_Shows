using Agenda_Shows.Services.Time;

namespace Agenda_Shows.Tests.Time;

/// <summary>
/// O "hoje" é o dia de Brasília. O processo roda em UTC: às 22h de Brasília o UTC já é o dia seguinte, e um filtro
/// pelo dia UTC mostraria os eventos de amanhã na hora de pico de quem procura o que fazer.
/// </summary>
public class QuandoDoEventoTests
{
    // 18/10/2026 (domingo) às 22:00 de Brasília = 19/10 01:00 UTC.
    private static readonly DateTime DomingoAs22DeBrasilia = new(2026, 10, 19, 1, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void As22hDeBrasilia_AindaEhOMesmoDia()
    {
        Assert.Equal(new DateOnly(2026, 10, 18), BrazilTime.DataEmBrasilia(DomingoAs22DeBrasilia));
        Assert.NotEqual(DateOnly.FromDateTime(DomingoAs22DeBrasilia), BrazilTime.DataEmBrasilia(DomingoAs22DeBrasilia));
    }

    [Fact]
    public void JanelaDeHoje_VaiDaMeiaNoiteAMeiaNoiteDeBrasilia()
    {
        var (de, ate) = QuandoDoEvento.Hoje(DomingoAs22DeBrasilia);

        Assert.Equal(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc), de);
        Assert.Equal(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc), ate);
    }

    [Fact]
    public void EventoAs23hDeBrasilia_VistoAs22h_EhHoje()
    {
        var as23 = new DateTime(2026, 10, 19, 2, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Hoje · 23:00", QuandoDoEvento.Rotulo(as23, DomingoAs22DeBrasilia));
    }

    [Fact]
    public void EventoDaMadrugadaSeguinte_EhAmanha()
    {
        var segundaAs01 = new DateTime(2026, 10, 19, 4, 0, 0, DateTimeKind.Utc);
        Assert.Equal("Amanhã · 01:00", QuandoDoEvento.Rotulo(segundaAs01, DomingoAs22DeBrasilia));
    }

    [Fact]
    public void DentroDaSemana_EscreveODia_DepoisADataCurta()
    {
        var sabado = new DateTime(2026, 10, 24, 13, 0, 0, DateTimeKind.Utc);   // sáb 10:00
        var daquiAQuinzeDias = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc); // 01/11 21:00

        Assert.Equal("Sáb · 10:00", QuandoDoEvento.Rotulo(sabado, DomingoAs22DeBrasilia));
        Assert.Equal("01/11 · 21:00", QuandoDoEvento.Rotulo(daquiAQuinzeDias, DomingoAs22DeBrasilia));
    }

    [Fact]
    public void FimDeSemana_NoDomingo_EhOAtual_ComecandoAgora()
    {
        var (de, ate) = QuandoDoEvento.FimDeSemana(DomingoAs22DeBrasilia);

        Assert.Equal(DomingoAs22DeBrasilia, de);
        Assert.Equal(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc), ate);   // segunda 00:00 de Brasília
    }

    [Fact]
    public void FimDeSemana_NumaTerca_EhOProximo()
    {
        var terca = new DateTime(2026, 10, 20, 15, 0, 0, DateTimeKind.Utc);
        var (de, ate) = QuandoDoEvento.FimDeSemana(terca);

        Assert.Equal(new DateTime(2026, 10, 23, 3, 0, 0, DateTimeKind.Utc), de);    // sexta 00:00
        Assert.Equal(new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc), ate);   // segunda 00:00
    }

    [Fact]
    public void HoraDigitadaNoFormulario_VoltaPraUtc()
    {
        var local = new DateTime(2026, 10, 24, 20, 30, 0);
        var utc = BrazilTime.ToUtc(local);

        Assert.Equal(new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal("2026-10-24T20:30", BrazilTime.ParaInputLocal(utc));
    }
}
