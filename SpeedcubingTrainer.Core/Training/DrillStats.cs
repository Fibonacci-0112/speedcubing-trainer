using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Core.Training;

public static class DrillStats
{
    public const int RecentLimit = 20;

    public static void Record(CaseProgressRecord record, DrillAttemptRecord attempt)
    {
        record.DrillAttempts++;
        if (attempt.Success)
        {
            record.DrillSuccesses++;
            record.BestRecognitionMs = Min(record.BestRecognitionMs, attempt.RecognitionMs);
            record.BestExecutionMs = Min(record.BestExecutionMs, attempt.ExecutionMs);
        }
        record.RecentDrills.Add(attempt);
        if (record.RecentDrills.Count > RecentLimit)
        {
            record.RecentDrills.RemoveRange(0, record.RecentDrills.Count - RecentLimit);
        }
    }

    public static double? MeanRecognitionMs(CaseProgressRecord record) => Mean(record.RecentDrills.Where(a => a.Success).Select(a => a.RecognitionMs));

    public static double? MeanExecutionMs(CaseProgressRecord record) => Mean(record.RecentDrills.Where(a => a.Success).Select(a => a.ExecutionMs));

    public static double? MeanTotalMs(CaseProgressRecord record) => Mean(record.RecentDrills.Where(a => a.Success).Select(a => a.RecognitionMs + a.ExecutionMs));

    private static int? Min(int? current, int candidate) => current is null ? candidate : Math.Min(current.Value, candidate);

    private static double? Mean(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? null : list.Average();
    }
}
