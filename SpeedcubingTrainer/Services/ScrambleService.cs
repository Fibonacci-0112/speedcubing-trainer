using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Solver;

namespace SpeedcubingTrainer.Services;

/// <summary>
/// Hands out scrambles for the timer. Random-move scrambles are served until the solver tables are
/// ready; after that random-state scrambles are used and the next one is always prepared in advance.
/// </summary>
public sealed partial class ScrambleService : ObservableObject
{
    private readonly AppDataService _data;
    private readonly RandomMoveScrambler _randomMoves = new(SystemRandom.Instance);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SolverTableProvider? _tables;
    private RandomStateScrambler? _randomState;
    private Task<Scramble>? _prefetched;

    [ObservableProperty]
    public partial bool IsSolverReady { get; private set; }

    [ObservableProperty]
    public partial double SolverProgress { get; private set; }

    [ObservableProperty]
    public partial ScrambleKind PreferredKind { get; set; } = ScrambleKind.RandomState;

    public ScrambleService(AppDataService data)
    {
        _data = data;
        _ = WarmUpAsync();
    }

    /// <summary>The kind of scramble the next call will actually produce.</summary>
    public ScrambleKind EffectiveKind => PreferredKind == ScrambleKind.RandomState && IsSolverReady
        ? ScrambleKind.RandomState
        : ScrambleKind.RandomMoves;

    public async Task<Scramble> NextAsync(CancellationToken cancellationToken = default)
    {
        if (EffectiveKind == ScrambleKind.RandomMoves)
        {
            return _randomMoves.Next();
        }

        Task<Scramble> pending;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            pending = _prefetched ?? GenerateRandomStateAsync();
            _prefetched = GenerateRandomStateAsync();
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            return await pending.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The solver should never fail for a valid random state, but a scramble must always be available.
            return _randomMoves.Next();
        }
    }

    private Task<Scramble> GenerateRandomStateAsync() =>
        _randomState is { } scrambler
            ? scrambler.NextAsync().AsTask()
            : Task.FromResult(_randomMoves.Next());

    private async Task WarmUpAsync()
    {
        try
        {
            var storage = await _data.GetStorageAsync();
            _tables = new SolverTableProvider(storage);
            var progress = new Progress<double>(p => SolverProgress = p);
            await _tables.GetAsync(progress);
            _randomState = new RandomStateScrambler(_tables, SystemRandom.Instance);
            IsSolverReady = true;
            OnPropertyChanged(nameof(EffectiveKind));
            _prefetched = GenerateRandomStateAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Solver tables unavailable, using random-move scrambles: {ex}");
        }
    }

    partial void OnPreferredKindChanged(ScrambleKind value) => OnPropertyChanged(nameof(EffectiveKind));
}
