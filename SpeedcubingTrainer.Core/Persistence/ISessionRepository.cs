using SpeedcubingTrainer.Core.Scrambling;

namespace SpeedcubingTrainer.Core.Persistence;

public enum ImportMode
{
    /// <summary>Add the imported sessions next to the existing ones (sessions with the same id are replaced).</summary>
    Merge,
    /// <summary>Delete everything first.</summary>
    Replace,
}

public interface ISessionRepository
{
    Task<IReadOnlyList<SessionInfo>> ListAsync(CancellationToken cancellationToken = default);

    Task<Session?> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Returns the active session, creating a default one when none exists.</summary>
    Task<Session> GetActiveAsync(CancellationToken cancellationToken = default);

    Task SetActiveAsync(string id, CancellationToken cancellationToken = default);

    Task<Session> CreateAsync(string name, ScrambleKind scrambleKind = ScrambleKind.RandomState, CancellationToken cancellationToken = default);

    Task RenameAsync(string id, string name, CancellationToken cancellationToken = default);

    Task DeleteSessionAsync(string id, CancellationToken cancellationToken = default);

    Task AddSolveAsync(string sessionId, SolveRecord solve, CancellationToken cancellationToken = default);

    Task UpdateSolveAsync(string sessionId, SolveRecord solve, CancellationToken cancellationToken = default);

    Task DeleteSolveAsync(string sessionId, string solveId, CancellationToken cancellationToken = default);

    Task ClearSolvesAsync(string sessionId, CancellationToken cancellationToken = default);

    Task<string> ExportJsonAsync(CancellationToken cancellationToken = default);

    Task<int> ImportJsonAsync(string json, ImportMode mode, CancellationToken cancellationToken = default);

    /// <summary>Raised after any change so views can refresh.</summary>
    event Action? Changed;
}
