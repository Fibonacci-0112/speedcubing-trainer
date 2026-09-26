using System.Text.Json;
using SpeedcubingTrainer.Core.Scrambling;

namespace SpeedcubingTrainer.Core.Persistence;

/// <summary>
/// Stores each session as <c>sessions/{id}.json</c> plus an index file. Every change is written through
/// immediately; the files are small enough that this is cheaper than risking a lost solve.
/// </summary>
public sealed class JsonSessionRepository(IAppStorage storage, TimeProvider? timeProvider = null) : ISessionRepository
{
    public const string IndexName = "sessions/index.json";
    public const string DefaultSessionName = "Session 1";

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, Session> _sessions = new();
    private SessionIndex? _index;

    public event Action? Changed;

    public async Task<IReadOnlyList<SessionInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            return index.Sessions.ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Session?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadSessionAsync(id, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Session> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.ActiveSessionId is { } activeId
                && await LoadSessionAsync(activeId, cancellationToken).ConfigureAwait(false) is { } active)
            {
                return active;
            }
            foreach (var info in index.Sessions)
            {
                if (await LoadSessionAsync(info.Id, cancellationToken).ConfigureAwait(false) is { } first)
                {
                    index.ActiveSessionId = first.Id;
                    await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
                    return first;
                }
            }
            var created = await CreateCoreAsync(DefaultSessionName, ScrambleKind.RandomState, cancellationToken).ConfigureAwait(false);
            index.ActiveSessionId = created.Id;
            await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
            return created;
        }
        finally
        {
            _lock.Release();
        }
        
    }

    public async Task SetActiveAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (index.Sessions.All(s => s.Id != id))
            {
                throw new KeyNotFoundException($"Unknown session '{id}'.");
            }
            index.ActiveSessionId = id;
            await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    public async Task<Session> CreateAsync(string name, ScrambleKind scrambleKind = ScrambleKind.RandomState, CancellationToken cancellationToken = default)
    {
        Session session;
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session = await CreateCoreAsync(name, scrambleKind, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
        return session;
    }

    public Task RenameAsync(string id, string name, CancellationToken cancellationToken = default) =>
        MutateAsync(id, session => session.Name = string.IsNullOrWhiteSpace(name) ? session.Name : name.Trim(), cancellationToken);

    public async Task DeleteSessionAsync(string id, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            index.Sessions.RemoveAll(s => s.Id == id);
            if (index.ActiveSessionId == id)
            {
                index.ActiveSessionId = index.Sessions.FirstOrDefault()?.Id;
            }
            _sessions.Remove(id);
            await storage.DeleteAsync(SessionName(id), cancellationToken).ConfigureAwait(false);
            await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    public Task AddSolveAsync(string sessionId, SolveRecord solve, CancellationToken cancellationToken = default) =>
        MutateAsync(sessionId, session => session.Solves.Add(solve), cancellationToken);

    public Task UpdateSolveAsync(string sessionId, SolveRecord solve, CancellationToken cancellationToken = default) =>
        MutateAsync(sessionId, session =>
        {
            var i = session.Solves.FindIndex(s => s.Id == solve.Id);
            if (i < 0)
            {
                throw new KeyNotFoundException($"Unknown solve '{solve.Id}'.");
            }
            session.Solves[i] = solve;
        }, cancellationToken);

    public Task DeleteSolveAsync(string sessionId, string solveId, CancellationToken cancellationToken = default) =>
        MutateAsync(sessionId, session => session.Solves.RemoveAll(s => s.Id == solveId), cancellationToken);

    public Task ClearSolvesAsync(string sessionId, CancellationToken cancellationToken = default) =>
        MutateAsync(sessionId, session => session.Solves.Clear(), cancellationToken);

    public async Task<string> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            var bundle = new ExportBundle { ExportedAt = _time.GetUtcNow() };
            foreach (var info in index.Sessions)
            {
                if (await LoadSessionAsync(info.Id, cancellationToken).ConfigureAwait(false) is { } session)
                {
                    bundle.Sessions.Add(session);
                }
            }
            var progressJson = await storage.ReadTextAsync(JsonAlgorithmProgressRepository.FileName, cancellationToken).ConfigureAwait(false);
            if (progressJson is not null)
            {
                bundle.AlgorithmProgress = JsonSerializer.Deserialize(progressJson, CoreJsonContext.Default.AlgorithmProgressFile);
            }
            return JsonSerializer.Serialize(bundle, CoreJsonContext.Default.ExportBundle);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> ImportJsonAsync(string json, ImportMode mode, CancellationToken cancellationToken = default)
    {
        var bundle = JsonSerializer.Deserialize(json, CoreJsonContext.Default.ExportBundle)
                     ?? throw new FormatException("The file is not a Speedcubing Trainer export.");
        if (bundle.SchemaVersion > ExportBundle.CurrentSchemaVersion)
        {
            throw new FormatException($"The export was made by a newer version (schema {bundle.SchemaVersion}).");
        }
        var imported = 0;
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            if (mode == ImportMode.Replace)
            {
                foreach (var info in index.Sessions)
                {
                    await storage.DeleteAsync(SessionName(info.Id), cancellationToken).ConfigureAwait(false);
                }
                index.Sessions.Clear();
                index.ActiveSessionId = null;
                _sessions.Clear();
            }
            foreach (var session in bundle.Sessions)
            {
                if (string.IsNullOrEmpty(session.Id) || string.IsNullOrEmpty(session.Name))
                {
                    continue;
                }
                session.SchemaVersion = Session.CurrentSchemaVersion;
                _sessions[session.Id] = session;
                await SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
                index.Sessions.RemoveAll(s => s.Id == session.Id);
                index.Sessions.Add(session.ToInfo());
                imported++;
            }
            index.ActiveSessionId ??= index.Sessions.FirstOrDefault()?.Id;
            await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
            if (bundle.AlgorithmProgress is { } progress)
            {
                await storage.WriteTextAsync(
                    JsonAlgorithmProgressRepository.FileName,
                    JsonSerializer.Serialize(progress, CoreJsonContext.Default.AlgorithmProgressFile),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
        return imported;
    }

    private async Task MutateAsync(string sessionId, Action<Session> mutate, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadSessionAsync(sessionId, cancellationToken).ConfigureAwait(false)
                          ?? throw new KeyNotFoundException($"Unknown session '{sessionId}'.");
            mutate(session);
            await SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
            var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
            var i = index.Sessions.FindIndex(s => s.Id == sessionId);
            if (i >= 0)
            {
                index.Sessions[i] = session.ToInfo();
                await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke();
    }

    private async Task<Session> CreateCoreAsync(string name, ScrambleKind scrambleKind, CancellationToken cancellationToken)
    {
        var index = await LoadIndexAsync(cancellationToken).ConfigureAwait(false);
        var session = new Session
        {
            Id = Ids.New(),
            Name = string.IsNullOrWhiteSpace(name) ? $"Session {index.Sessions.Count + 1}" : name.Trim(),
            CreatedAt = _time.GetUtcNow(),
            ScrambleKind = scrambleKind,
        };
        _sessions[session.Id] = session;
        await SaveSessionAsync(session, cancellationToken).ConfigureAwait(false);
        index.Sessions.Add(session.ToInfo());
        index.ActiveSessionId ??= session.Id;
        await SaveIndexAsync(index, cancellationToken).ConfigureAwait(false);
        return session;
    }

    private static string SessionName(string id) => $"sessions/{id}.json";

    private async Task<SessionIndex> LoadIndexAsync(CancellationToken cancellationToken)
    {
        if (_index is not null)
        {
            return _index;
        }
        var json = await storage.ReadTextAsync(IndexName, cancellationToken).ConfigureAwait(false);
        SessionIndex? index = null;
        if (json is not null)
        {
            try
            {
                index = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SessionIndex);
            }
            catch (JsonException)
            {
                index = null;
            }
        }
        index ??= await RebuildIndexAsync(cancellationToken).ConfigureAwait(false);
        if (index.SchemaVersion > SessionIndex.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Session index schema {index.SchemaVersion} is newer than this app supports.");
        }
        _index = index;
        return index;
    }

    /// <summary>Recovers the index from the session files when it is missing or unreadable.</summary>
    private async Task<SessionIndex> RebuildIndexAsync(CancellationToken cancellationToken)
    {
        var index = new SessionIndex();
        foreach (var name in await storage.ListAsync("sessions/", cancellationToken).ConfigureAwait(false))
        {
            if (name == IndexName || !name.EndsWith(".json", StringComparison.Ordinal))
            {
                continue;
            }
            var id = Path.GetFileNameWithoutExtension(name);
            if (await LoadSessionAsync(id, cancellationToken).ConfigureAwait(false) is { } session)
            {
                index.Sessions.Add(session.ToInfo());
            }
        }
        index.Sessions.Sort((a, b) => a.CreatedAt.CompareTo(b.CreatedAt));
        index.ActiveSessionId = index.Sessions.FirstOrDefault()?.Id;
        return index;
    }

    private Task SaveIndexAsync(SessionIndex index, CancellationToken cancellationToken)
    {
        _index = index;
        return storage.WriteTextAsync(IndexName, JsonSerializer.Serialize(index, CoreJsonContext.Default.SessionIndex), cancellationToken);
    }

    private async Task<Session?> LoadSessionAsync(string id, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(id, out var cached))
        {
            return cached;
        }
        var json = await storage.ReadTextAsync(SessionName(id), cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return null;
        }
        var session = JsonSerializer.Deserialize(json, CoreJsonContext.Default.Session);
        if (session is null)
        {
            return null;
        }
        if (session.SchemaVersion > Session.CurrentSchemaVersion)
        {
            throw new InvalidOperationException($"Session schema {session.SchemaVersion} is newer than this app supports.");
        }
        _sessions[id] = session;
        return session;
    }

    private Task SaveSessionAsync(Session session, CancellationToken cancellationToken) =>
        storage.WriteTextAsync(SessionName(session.Id), JsonSerializer.Serialize(session, CoreJsonContext.Default.Session), cancellationToken);
}
