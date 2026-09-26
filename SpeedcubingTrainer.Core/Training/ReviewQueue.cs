using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Core.Training;

/// <summary>Picks which cases to review today: everything due, then new cases up to a daily limit.</summary>
public static class ReviewQueue
{
    public sealed record Entry(AlgCase Case, CaseProgressRecord Progress, bool IsNew);

    public static IReadOnlyList<Entry> Build(
        IEnumerable<AlgCase> cases,
        IReadOnlyDictionary<string, CaseProgressRecord> progress,
        DateOnly today,
        int newPerDay,
        Func<AlgCase, CaseProgressRecord, bool>? filter = null)
    {
        var due = new List<Entry>();
        var fresh = new List<Entry>();
        foreach (var c in cases)
        {
            var record = progress.TryGetValue(c.Id, out var p) ? p : new CaseProgressRecord();
            if (filter is not null && !filter(c, record))
            {
                continue;
            }
            var state = ReviewState.From(record);
            if (state.IsNew)
            {
                fresh.Add(new Entry(c, record, true));
            }
            else if (state.IsDue(today))
            {
                due.Add(new Entry(c, record, false));
            }
        }
        due.Sort((a, b) => Nullable.Compare(a.Progress.Due, b.Progress.Due));
        return due.Concat(fresh.Take(Math.Max(0, newPerDay))).ToList();
    }

    public static int CountDue(IEnumerable<AlgCase> cases, IReadOnlyDictionary<string, CaseProgressRecord> progress, DateOnly today) =>
        cases.Count(c => progress.TryGetValue(c.Id, out var p) && ReviewState.From(p).IsDue(today));
}
