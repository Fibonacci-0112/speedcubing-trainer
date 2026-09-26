using System.Text.Json;

namespace SpeedcubingTrainer.Core.Persistence;

public interface IAlgorithmProgressRepository
{
    Task<AlgorithmProgressFile> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the record for a case, creating an empty one if needed.</summary>
    Task<CaseProgressRecord> GetAsync(string caseId, CancellationToken cancellationToken = default);

    Task SaveAsync(string caseId, CaseProgressRecord record, CancellationToken cancellationToken = default);

    Task ResetAsync(CancellationToken cancellationToken = default);

    event Action? Changed;
}

public sealed class JsonAlgorithmProgressRepository(IAppStorage storage) : IAlgorithmProgressRepository
{
    public const string FileName = "progress/algorithms.json";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private AlgorithmProgressFile? _file;

    public event Action? Changed;

    public async Task<AlgorithmProgressFile> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<CaseProgressRecord> GetAsync(string caseId, CancellationToken cancellationToken = default)
    {
        var file = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return file.Cases.TryGetValue(caseId, out var record) ? record : new CaseProgressRecord();
    }

    public async Task SaveAsync(string caseId, CaseProgressRecord record, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var file = await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
            file.Cases[caseId] = record;
            await storage.WriteTextAsync(FileName, JsonSerializer.Serialize(file, CoreJsonContext.Default.AlgorithmProgressFile), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _file = new AlgorithmProgressFile();
            await storage.DeleteAsync(FileName, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    /// <summary>Drops the in-memory copy so the next read comes from storage (used after an import).</summary>
    public void Invalidate() => _file = null;

    private async Task<AlgorithmProgressFile> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (_file is not null)
        {
            return _file;
        }
        var json = await storage.ReadTextAsync(FileName, cancellationToken).ConfigureAwait(false);
        AlgorithmProgressFile? file = null;
        if (json is not null)
        {
            try
            {
                file = JsonSerializer.Deserialize(json, CoreJsonContext.Default.AlgorithmProgressFile);
            }
            catch (JsonException)
            {
                file = null;
            }
        }
        file ??= new AlgorithmProgressFile();
        if (file.SchemaVersion > AlgorithmProgressFile.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Progress schema {file.SchemaVersion} is newer than this app supports.");
        }
        _file = file;
        return file;
    }
}
