using System.Diagnostics;
using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Core.Solver;

/// <summary>
/// Lazily builds the solver tables once, caching the serialized blob through <see cref="IBlobStorage"/>
/// so later launches skip the build. Concurrent callers share one build.
/// </summary>
public sealed class SolverTableProvider(IBlobStorage? cache = null, bool? useThreadPool = null)
{
    public const string CacheKey = "solver/twophase-v1.bin";

    private readonly bool _useThreadPool = useThreadPool ?? !OperatingSystem.IsBrowser();
    private readonly object _gate = new();
    private Task<SolverTables>? _loading;

    public SolverTables? Current { get; private set; }

    public bool IsReady => Current is not null;

    /// <summary>Time the last table build took, or null if the tables came from the cache.</summary>
    public TimeSpan? LastBuildDuration { get; private set; }

    public event Action? Ready;

    public Task<SolverTables> GetAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (Current is { } ready)
        {
            return Task.FromResult(ready);
        }
        Task<SolverTables> loading;
        lock (_gate)
        {
            _loading ??= LoadAsync(progress);
            loading = _loading;
        }
        return loading.WaitAsync(cancellationToken);
    }

    private async Task<SolverTables> LoadAsync(IProgress<double>? progress)
    {
        try
        {
            var tables = await TryReadCacheAsync().ConfigureAwait(false);
            if (tables is null)
            {
                var stopwatch = Stopwatch.StartNew();
                tables = _useThreadPool
                    ? await Task.Run(() => BuildAsync(progress)).ConfigureAwait(false)
                    : await BuildAsync(progress).ConfigureAwait(false);
                LastBuildDuration = stopwatch.Elapsed;
                await TryWriteCacheAsync(tables).ConfigureAwait(false);
            }
            Current = tables;
            progress?.Report(1);
            Ready?.Invoke();
            return tables;
        }
        catch
        {
            lock (_gate)
            {
                _loading = null;
            }
            throw;
        }
    }

    /// <summary>Builds the tables, yielding to the scheduler roughly every 30 ms so a single-threaded host stays responsive.</summary>
    public static async Task<SolverTables> BuildAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var tables = new SolverTables();
        var slice = Stopwatch.StartNew();
        foreach (var p in TableBuilder.Build(tables))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(p);
            if (slice.ElapsedMilliseconds >= 30)
            {
                await Task.Yield();
                slice.Restart();
            }
        }
        return tables;
    }

    private async Task<SolverTables?> TryReadCacheAsync()
    {
        if (cache is null)
        {
            return null;
        }
        try
        {
            var blob = await cache.ReadBytesAsync(CacheKey).ConfigureAwait(false);
            return blob is null ? null : SolverTables.TryDeserialize(blob);
        }
        catch
        {
            return null;
        }
    }

    private async Task TryWriteCacheAsync(SolverTables tables)
    {
        if (cache is null)
        {
            return;
        }
        try
        {
            await cache.WriteBytesAsync(CacheKey, tables.Serialize()).ConfigureAwait(false);
        }
        catch
        {
            // A failed cache write only costs a rebuild next time.
        }
    }
}
