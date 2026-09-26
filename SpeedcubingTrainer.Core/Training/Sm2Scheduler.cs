using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Core.Training;

public enum Grade
{
    Again,
    Hard,
    Good,
    Easy,
}

/// <summary>Spaced-repetition state of one case: SM-2 with the usual Anki-style tweaks.</summary>
public sealed record ReviewState(double Ease = 2.5, int IntervalDays = 0, int Repetitions = 0, int Lapses = 0, DateOnly? Due = null)
{
    public const double MinimumEase = 1.3;

    public bool IsNew => Repetitions == 0 && Lapses == 0 && Due is null;

    public bool IsDue(DateOnly today) => Due is { } due && due <= today;

    public static ReviewState From(CaseProgressRecord record) =>
        new(record.Ease, record.IntervalDays, record.Repetitions, record.Lapses, record.Due);

    public void ApplyTo(CaseProgressRecord record)
    {
        record.Ease = Ease;
        record.IntervalDays = IntervalDays;
        record.Repetitions = Repetitions;
        record.Lapses = Lapses;
        record.Due = Due;
    }
}

public sealed class Sm2Scheduler(int maxIntervalDays = 365)
{
    public ReviewState Review(ReviewState state, Grade grade, DateOnly today)
    {
        var (interval, ease, reps, lapses) = Next(state, grade);
        return state with
        {
            Ease = ease,
            IntervalDays = interval,
            Repetitions = reps,
            Lapses = lapses,
            Due = today.AddDays(interval),
        };
    }

    /// <summary>Interval in days each grade would produce, for showing on the answer buttons.</summary>
    public IReadOnlyDictionary<Grade, int> PreviewIntervals(ReviewState state) =>
        Enum.GetValues<Grade>().ToDictionary(g => g, g => Next(state, g).Interval);

    private (int Interval, double Ease, int Repetitions, int Lapses) Next(ReviewState s, Grade grade)
    {
        var ease = s.Ease;
        int interval;
        var reps = s.Repetitions;
        var lapses = s.Lapses;
        switch (grade)
        {
            case Grade.Again:
                ease = Math.Max(ReviewState.MinimumEase, ease - 0.2);
                interval = 0;
                reps = 0;
                lapses++;
                break;
            case Grade.Hard:
                ease = Math.Max(ReviewState.MinimumEase, ease - 0.15);
                interval = Math.Max(s.IntervalDays + 1, (int)Math.Round(s.IntervalDays * 1.2));
                reps++;
                break;
            case Grade.Good:
                interval = reps switch
                {
                    0 => 1,
                    1 => 6,
                    _ => (int)Math.Round(s.IntervalDays * ease),
                };
                reps++;
                break;
            default:
                var good = reps switch { 0 => 1, 1 => 6, _ => (int)Math.Round(s.IntervalDays * ease) };
                interval = reps == 0 ? 4 : Math.Max(good + 1, (int)Math.Round(s.IntervalDays * ease * 1.3));
                ease += 0.15;
                reps++;
                break;
        }
        interval = Math.Clamp(interval, 0, maxIntervalDays);
        return (interval, Math.Round(ease, 2), reps, lapses);
    }
}
