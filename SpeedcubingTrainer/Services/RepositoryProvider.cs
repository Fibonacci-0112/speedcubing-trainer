using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Services;

/// <summary>Creates the repositories once the data folder is available.</summary>
public sealed class RepositoryProvider(AppDataService data)
{
    private readonly Lazy<Task<JsonSessionRepository>> _sessions = new(async () => new JsonSessionRepository(await data.GetStorageAsync()));
    private readonly Lazy<Task<JsonAlgorithmProgressRepository>> _progress = new(async () => new JsonAlgorithmProgressRepository(await data.GetStorageAsync()));

    public Task<JsonSessionRepository> GetSessionsAsync() => _sessions.Value;

    public Task<JsonAlgorithmProgressRepository> GetProgressAsync() => _progress.Value;
}
