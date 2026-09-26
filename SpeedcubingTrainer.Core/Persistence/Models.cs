using System.Text.Json.Serialization;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Statistics;

namespace SpeedcubingTrainer.Core.Persistence;

public static class Ids
{
    public static string New() => Guid.CreateVersion7().ToString("N");
}

public sealed record SolveRecord(
    string Id,
    DateTimeOffset At,
    int TimeMs,
    Penalty Penalty,
    string Scramble,
    string? Comment = null)
{
    [JsonIgnore]
    public SolveTime Time => new(TimeMs, Penalty);
}

public sealed record SessionInfo(
    string Id,
    string Name,
    DateTimeOffset CreatedAt,
    int SolveCount,
    DateTimeOffset? LastSolveAt);

/// <summary>On-disk shape of one session; also the in-memory session object.</summary>
public sealed class Session
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public required string Id { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ScrambleKind ScrambleKind { get; set; } = ScrambleKind.RandomState;

    public List<SolveRecord> Solves { get; set; } = [];

    [JsonIgnore]
    public IReadOnlyList<SolveTime> Times => Solves.Select(s => s.Time).ToList();

    public SessionInfo ToInfo() => new(Id, Name, CreatedAt, Solves.Count, Solves.Count == 0 ? null : Solves[^1].At);
}

public sealed class SessionIndex
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string? ActiveSessionId { get; set; }

    public List<SessionInfo> Sessions { get; set; } = [];
}

/// <summary>Everything the app stores, for backup and transfer between devices.</summary>
public sealed class ExportBundle
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public DateTimeOffset ExportedAt { get; set; }

    public List<Session> Sessions { get; set; } = [];

    public AlgorithmProgressFile? AlgorithmProgress { get; set; }
}

/// <summary>Per-case learning state for the algorithm trainer.</summary>
public sealed class AlgorithmProgressFile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public Dictionary<string, CaseProgressRecord> Cases { get; set; } = [];
}

public enum LearningStatus
{
    NotStarted,
    Learning,
    Learned,
}

public sealed class CaseProgressRecord
{
    public LearningStatus Status { get; set; }

    public bool Favorite { get; set; }

    /// <summary>Index into the case's algorithm list of the one the user prefers.</summary>
    public int PreferredAlgorithm { get; set; }

    public double Ease { get; set; } = 2.5;

    public int IntervalDays { get; set; }

    public int Repetitions { get; set; }

    public int Lapses { get; set; }

    public DateOnly? Due { get; set; }

    public DateTimeOffset? LastReviewed { get; set; }

    public int DrillAttempts { get; set; }

    public int DrillSuccesses { get; set; }

    public int? BestRecognitionMs { get; set; }

    public int? BestExecutionMs { get; set; }

    public List<DrillAttemptRecord> RecentDrills { get; set; } = [];

    public int QuizAsked { get; set; }

    public int QuizCorrect { get; set; }
}

public sealed record DrillAttemptRecord(DateTimeOffset At, int RecognitionMs, int ExecutionMs, bool Success);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(Session))]
[JsonSerializable(typeof(SessionIndex))]
[JsonSerializable(typeof(ExportBundle))]
[JsonSerializable(typeof(AlgorithmProgressFile))]
public sealed partial class CoreJsonContext : JsonSerializerContext
{
}
