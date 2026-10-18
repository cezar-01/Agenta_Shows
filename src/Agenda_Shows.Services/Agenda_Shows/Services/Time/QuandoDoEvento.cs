namespace Agenda_Shows.Services.Time;

/// <summary>
/// O "quando" de um evento, escrito para gente — o texto do card, do pino e da página ("Hoje · 20:30",
/// "Amanhã · 19:00", "Sáb · 10:00", "12/11 · 21:00") — e as janelas dos filtros "hoje", "fim de semana" e
/// "esta semana". Tudo pelo calendário de Brasília (<see cref="BrazilTime"/>), nunca pelo dia UTC.
/// </summary>
public static class QuandoDoEvento
{
    /// <summary>Até quantos dias à frente o card escreve o dia da semana em vez da data.</summary>
    public const int DiasComNomeDaSemana = 6;

    private static readonly string[] Semana = ["Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb"];

    /// <summary>"Hoje · 20:30" — o rótulo curto. Evento que já começou e segue hoje continua "Hoje".</summary>
    public static string Rotulo(DateTime inicioUtc, DateTime agoraUtc)
    {
        var hora = BrazilTime.Format(inicioUtc, "HH:mm");
        var dias = BrazilTime.DiasDeCalendario(agoraUtc, inicioUtc);
        var dia = dias switch
        {
            0 => "Hoje",
            1 => "Amanhã",
            -1 => "Ontem",
            > 1 and <= DiasComNomeDaSemana => Semana[(int)BrazilTime.ToBrasilia(inicioUtc).DayOfWeek],
            _ => BrazilTime.Format(inicioUtc, "dd/MM"),
        };
        return $"{dia} · {hora}";
    }

    /// <summary>A janela [início, fim) em UTC do dia de Brasília em que <paramref name="agoraUtc"/> cai.</summary>
    public static (DateTime DeUtc, DateTime AteUtc) Hoje(DateTime agoraUtc)
    {
        var hoje = BrazilTime.DataEmBrasilia(agoraUtc);
        return (BrazilTime.InicioDoDiaUtc(hoje), BrazilTime.InicioDoDiaUtc(hoje.AddDays(1)));
    }

    /// <summary>
    /// O fim de semana que interessa: de sexta 00h a segunda 00h de Brasília. Se hoje já é sexta, sábado ou domingo, é
    /// este; senão, o próximo. Começa em <paramref name="agoraUtc"/> quando ele já está dentro (não mostra o que passou).
    /// </summary>
    public static (DateTime DeUtc, DateTime AteUtc) FimDeSemana(DateTime agoraUtc)
    {
        var hoje = BrazilTime.DataEmBrasilia(agoraUtc);
        var deSexta = ((int)hoje.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;   // sex=0, sáb=1, dom=2, seg=3...
        var sexta = deSexta <= 2 ? hoje.AddDays(-deSexta) : hoje.AddDays(7 - deSexta);
        var de = BrazilTime.InicioDoDiaUtc(sexta);
        var ate = BrazilTime.InicioDoDiaUtc(sexta.AddDays(3));
        return (de < agoraUtc ? agoraUtc : de, ate);
    }

    /// <summary>De agora até o fim do 7º dia de Brasília (hoje incluso).</summary>
    public static (DateTime DeUtc, DateTime AteUtc) ProximosSeteDias(DateTime agoraUtc)
    {
        var hoje = BrazilTime.DataEmBrasilia(agoraUtc);
        return (agoraUtc, BrazilTime.InicioDoDiaUtc(hoje.AddDays(7)));
    }
}
