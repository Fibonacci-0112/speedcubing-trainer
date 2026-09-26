namespace SpeedcubingTrainer.Core.Statistics;

public enum StatKind
{
    Single,
    Mo3,
    Ao5,
    Ao12,
    Ao50,
    Ao100,
}

/// <summary>Averages and means following the WCA regulations for trimmed averages.</summary>
public static class WcaStatistics
{
    public static int WindowSize(StatKind kind) => kind switch
    {
        StatKind.Single => 1,
        StatKind.Mo3 => 3,
        StatKind.Ao5 => 5,
        StatKind.Ao12 => 12,
        StatKind.Ao50 => 50,
        StatKind.Ao100 => 100,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string Label(StatKind kind) => kind switch
    {
        StatKind.Single => "single",
        StatKind.Mo3 => "mo3",
        StatKind.Ao5 => "ao5",
        StatKind.Ao12 => "ao12",
        StatKind.Ao50 => "ao50",
        StatKind.Ao100 => "ao100",
        _ => kind.ToString(),
    };

    /// <summary>Number of results removed from each end of a trimmed average: 1 up to ao12, 5% (rounded up) beyond.</summary>
    public static int TrimCount(int windowSize) => windowSize <= 12 ? 1 : (int)Math.Ceiling(windowSize * 0.05);

    /// <summary>Trimmed average of a full window, or null (DNF) when too many solves are DNF. Rounded to the nearest ms.</summary>
    public static int? AverageOf(ReadOnlySpan<SolveTime> window)
    {
        var n = window.Length;
        if (n < 3)
        {
            throw new ArgumentException("An average needs at least three solves.", nameof(window));
        }
        var trim = TrimCount(n);
        var dnfCount = 0;
        Span<long> values = n <= 128 ? stackalloc long[n] : new long[n];
        for (var i = 0; i < n; i++)
        {
            var effective = window[i].EffectiveMs;
            if (effective is null)
            {
                dnfCount++;
                values[i] = long.MaxValue;
            }
            else
            {
                values[i] = effective.Value;
            }
        }
        if (dnfCount > trim)
        {
            return null;
        }
        values.Sort();
        long sum = 0;
        for (var i = trim; i < n - trim; i++)
        {
            sum += values[i];
        }
        var counted = n - 2 * trim;
        return (int)((sum + counted / 2) / counted);
    }

    /// <summary>Plain mean of a window; any DNF makes the mean a DNF.</summary>
    public static int? MeanOf(ReadOnlySpan<SolveTime> window)
    {
        if (window.IsEmpty)
        {
            return null;
        }
        long sum = 0;
        foreach (var time in window)
        {
            if (time.EffectiveMs is not { } ms)
            {
                return null;
            }
            sum += ms;
        }
        return (int)((sum + window.Length / 2) / window.Length);
    }

    /// <summary>Value of a statistic over the window ending at <paramref name="endIndex"/> (inclusive), or null if not enough solves.</summary>
    public static int? Rolling(IReadOnlyList<SolveTime> solves, StatKind kind, int endIndex, out bool hasWindow)
    {
        var size = WindowSize(kind);
        if (endIndex < size - 1 || endIndex >= solves.Count)
        {
            hasWindow = false;
            return null;
        }
        hasWindow = true;
        var window = new SolveTime[size];
        for (var i = 0; i < size; i++)
        {
            window[i] = solves[endIndex - size + 1 + i];
        }
        return kind switch
        {
            StatKind.Single => window[0].EffectiveMs,
            StatKind.Mo3 => MeanOf(window),
            _ => AverageOf(window),
        };
    }
}

/// <summary>Best value of a statistic and the index of the last solve in its window.</summary>
public sealed record BestRecord(int Ms, int EndIndex);

/// <summary>Current and best value of one statistic over a session.</summary>
public sealed record StatSnapshot(StatKind Kind, int? Current, bool HasCurrent, BestRecord? Best);

public sealed record SessionSummary(
    int Count,
    int DnfCount,
    int? Mean,
    int? BestSingle,
    int? WorstSingle,
    IReadOnlyList<StatSnapshot> Stats);

public static class SessionStatistics
{
    public static readonly StatKind[] Kinds = [StatKind.Single, StatKind.Mo3, StatKind.Ao5, StatKind.Ao12, StatKind.Ao50, StatKind.Ao100];

    public static SessionSummary Summarize(IReadOnlyList<SolveTime> solves)
    {
        var stats = new List<StatSnapshot>(Kinds.Length);
        foreach (var kind in Kinds)
        {
            var current = WcaStatistics.Rolling(solves, kind, solves.Count - 1, out var hasCurrent);
            BestRecord? best = null;
            var size = WcaStatistics.WindowSize(kind);
            for (var end = size - 1; end < solves.Count; end++)
            {
                var value = WcaStatistics.Rolling(solves, kind, end, out _);
                if (value is { } ms && (best is null || ms < best.Ms))
                {
                    best = new BestRecord(ms, end);
                }
            }
            stats.Add(new StatSnapshot(kind, current, hasCurrent, best));
        }

        long sum = 0;
        var counted = 0;
        var dnf = 0;
        int? worst = null;
        foreach (var solve in solves)
        {
            if (solve.EffectiveMs is { } ms)
            {
                sum += ms;
                counted++;
                worst = worst is null ? ms : Math.Max(worst.Value, ms);
            }
            else
            {
                dnf++;
            }
        }
        var mean = counted == 0 ? (int?)null : (int)((sum + counted / 2) / counted);
        var bestSingle = stats[0].Best?.Ms;
        return new SessionSummary(solves.Count, dnf, mean, bestSingle, worst, stats);
    }

    /// <summary>Rolling series of a statistic aligned with the solve list (null where no window exists or the value is DNF).</summary>
    public static int?[] Series(IReadOnlyList<SolveTime> solves, StatKind kind)
    {
        var result = new int?[solves.Count];
        for (var i = 0; i < solves.Count; i++)
        {
            result[i] = WcaStatistics.Rolling(solves, kind, i, out _);
        }
        return result;
    }
}
