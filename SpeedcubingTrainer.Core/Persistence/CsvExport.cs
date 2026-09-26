using System.Globalization;
using System.Text;
using SpeedcubingTrainer.Core.Statistics;

namespace SpeedcubingTrainer.Core.Persistence;

public static class CsvExport
{
    /// <summary>One row per solve: index, date, time in seconds, penalty, effective time, scramble, comment.</summary>
    public static string Session(Session session)
    {
        var sb = new StringBuilder();
        sb.AppendLine("No,Date,Time,Penalty,Effective,Scramble,Comment");
        for (var i = 0; i < session.Solves.Count; i++)
        {
            var solve = session.Solves[i];
            var time = solve.Time;
            sb.Append(i + 1).Append(',')
              .Append(solve.At.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
              .Append((solve.TimeMs / 1000.0).ToString("0.000", CultureInfo.InvariantCulture)).Append(',')
              .Append(time.Penalty switch { Penalty.Plus2 => "+2", Penalty.Dnf => "DNF", _ => "" }).Append(',')
              .Append(time.EffectiveMs is { } ms ? (ms / 1000.0).ToString("0.000", CultureInfo.InvariantCulture) : "DNF").Append(',')
              .Append(Quote(solve.Scramble)).Append(',')
              .Append(Quote(solve.Comment ?? string.Empty))
              .AppendLine();
        }
        return sb.ToString();
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
