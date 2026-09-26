namespace SpeedcubingTrainer.Core.Statistics;

public enum Penalty : byte
{
    None,
    Plus2,
    Dnf,
}

/// <summary>A solve result in milliseconds together with its penalty.</summary>
public readonly record struct SolveTime(int Ms, Penalty Penalty = Penalty.None)
{
    public const int Plus2Ms = 2000;

    public bool IsDnf => Penalty == Penalty.Dnf;

    /// <summary>Time that counts for statistics: null for DNF, +2000 ms for a +2.</summary>
    public int? EffectiveMs => Penalty switch
    {
        Penalty.Dnf => null,
        Penalty.Plus2 => Ms + Plus2Ms,
        _ => Ms,
    };

    public static SolveTime Dnf(int ms) => new(ms, Penalty.Dnf);
}

/// <summary>Formats times the way timers display them: m:ss.cc, with DNF and +2 markers.</summary>
public static class TimeFormat
{
    public static string Format(int ms, bool showHundredths = true)
    {
        var totalHundredths = ms / 10;
        var minutes = totalHundredths / 6000;
        var seconds = totalHundredths / 100 % 60;
        var hundredths = totalHundredths % 100;
        var body = minutes > 0 ? $"{minutes}:{seconds:00}" : seconds.ToString();
        return showHundredths ? $"{body}.{hundredths:00}" : body;
    }

    public static string Format(SolveTime time) => time.Penalty switch
    {
        Penalty.Dnf => "DNF",
        Penalty.Plus2 => Format(time.Ms + SolveTime.Plus2Ms) + "+",
        _ => Format(time.Ms),
    };

    public static string Format(int? ms) => ms is null ? "DNF" : Format(ms.Value);

    public static string FormatOrDash(int? ms) => ms is null ? "-" : Format(ms.Value);
}
