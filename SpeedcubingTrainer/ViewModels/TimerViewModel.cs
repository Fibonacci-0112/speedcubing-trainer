using System.Diagnostics;
using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Statistics;
using SpeedcubingTrainer.Services;

namespace SpeedcubingTrainer.ViewModels;

public enum TimerState
{
    Idle,
    /// <summary>Pressed while idle or inspecting; waiting to see whether the hold is long enough.</summary>
    Holding,
    Inspecting,
    Running,
    /// <summary>Stopped but the stopping press has not been released yet.</summary>
    Stopped,
}

public enum TimerVisual
{
    Normal,
    HoldWarming,
    HoldReady,
    Inspection,
    InspectionPenalty,
    Running,
    Result,
}

public sealed record StatRow(string Label, string Current, string Best);

public sealed partial class TimerViewModel : ObservableObject
{
    private const int InspectionSeconds = 15;
    private const int Plus2AtSeconds = 15;
    private const int DnfAtSeconds = 17;

    private readonly ScrambleService _scrambles;
    private readonly RepositoryProvider _repositories;
    private readonly SettingsService _settings;
    private readonly Stopwatch _stopwatch = new();
    private readonly Stopwatch _inspection = new();
    private readonly Stopwatch _hold = new();
    private Session? _session;
    private Penalty _inspectionPenalty;
    private bool _holdStartsRun;
    private int _scrambleRequest;

    public TimerViewModel(ScrambleService scrambles, RepositoryProvider repositories, SettingsService settings)
    {
        _scrambles = scrambles;
        _repositories = repositories;
        _settings = settings;
        _scrambles.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ScrambleService.IsSolverReady) or nameof(ScrambleService.EffectiveKind))
            {
                OnPropertyChanged(nameof(ScrambleKindLabel));
                OnPropertyChanged(nameof(SolverStatus));
            }
        };
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.ScrambleKind))
            {
                _scrambles.PreferredKind = _settings.ScrambleKind;
                _ = NewScrambleAsync();
            }
            if (e.PropertyName == nameof(SettingsService.NotationStyle))
            {
                OnPropertyChanged(nameof(ScrambleText));
            }
        };
        _scrambles.PreferredKind = _settings.ScrambleKind;
        _ = InitializeAsync();
    }

    [ObservableProperty]
    public partial TimerState State { get; private set; }

    [ObservableProperty]
    public partial TimerVisual Visual { get; private set; }

    [ObservableProperty]
    public partial string Display { get; private set; } = "0.00";

    [ObservableProperty]
    public partial Scramble? CurrentScramble { get; private set; }

    [ObservableProperty]
    public partial bool IsLoadingScramble { get; private set; }

    [ObservableProperty]
    public partial SolveRecord? LastSolve { get; private set; }

    [ObservableProperty]
    public partial string SessionName { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string SessionSummaryText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<StatRow> Stats { get; private set; } = [];

    [ObservableProperty]
    public partial string LastSolveText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasLastSolve { get; private set; }

    [ObservableProperty]
    public partial bool IsLastPlus2 { get; private set; }

    [ObservableProperty]
    public partial bool IsLastDnf { get; private set; }

    public string ScrambleText => CurrentScramble?.Algorithm.ToString(_settings.NotationStyle) ?? string.Empty;

    public CubeState? PreviewState => CurrentScramble?.State;

    public string ScrambleKindLabel => CurrentScramble?.Kind == ScrambleKind.RandomState ? "random state" : "random moves";

    public string SolverStatus => _scrambles.IsSolverReady
        ? string.Empty
        : $"Preparing random-state scrambler… {_scrambles.SolverProgress:P0}";

    public bool ShowPreview => _settings.ShowScramblePreview;

    public double TimerFontSize => _settings.TimerFontSize;

    public bool IsTiming => State is TimerState.Inspecting or TimerState.Running or TimerState.Holding;

    public string SessionId => _session?.Id ?? string.Empty;

    private async Task InitializeAsync()
    {
        var repo = await _repositories.GetSessionsAsync();
        repo.Changed += () => _ = RefreshSessionAsync();
        await RefreshSessionAsync();
        await NewScrambleAsync();
    }

    public async Task RefreshSessionAsync()
    {
        var repo = await _repositories.GetSessionsAsync();
        _session = await repo.GetActiveAsync();
        SessionName = _session.Name;
        LastSolve = _session.Solves.Count > 0 ? _session.Solves[^1] : null;
        UpdateLastSolve();
        UpdateStats();
        if (State == TimerState.Idle && LastSolve is { } last)
        {
            Display = TimeFormat.Format(last.Time);
        }
    }

    [RelayCommand]
    public async Task NewScrambleAsync()
    {
        var request = ++_scrambleRequest;
        IsLoadingScramble = true;
        try
        {
            var scramble = await _scrambles.NextAsync();
            if (request == _scrambleRequest)
            {
                CurrentScramble = scramble;
            }
        }
        finally
        {
            if (request == _scrambleRequest)
            {
                IsLoadingScramble = false;
            }
        }
    }

    partial void OnCurrentScrambleChanged(Scramble? value)
    {
        OnPropertyChanged(nameof(ScrambleText));
        OnPropertyChanged(nameof(PreviewState));
        OnPropertyChanged(nameof(ScrambleKindLabel));
    }

    partial void OnStateChanged(TimerState value) => OnPropertyChanged(nameof(IsTiming));

    /// <summary>Space bar or finger goes down.</summary>
    public void Press()
    {
        switch (State)
        {
            case TimerState.Idle:
                if (_settings.InspectionEnabled)
                {
                    // A press-and-release starts inspection; the run starts from the next press.
                    _holdStartsRun = false;
                }
                else
                {
                    _holdStartsRun = true;
                }
                BeginHold();
                break;
            case TimerState.Inspecting:
                _holdStartsRun = true;
                BeginHold();
                break;
            case TimerState.Running:
                Stop();
                break;
        }
    }

    /// <summary>Space bar or finger comes up.</summary>
    public void Release()
    {
        switch (State)
        {
            case TimerState.Holding:
                _hold.Stop();
                if (!_holdStartsRun)
                {
                    StartInspection();
                }
                else if (_hold.ElapsedMilliseconds >= _settings.HoldToStartMs)
                {
                    StartRun();
                }
                else
                {
                    // Released too early: go back to where we were.
                    if (_inspection.IsRunning)
                    {
                        State = TimerState.Inspecting;
                        Tick();
                    }
                    else
                    {
                        State = TimerState.Idle;
                        Visual = TimerVisual.Normal;
                        Display = LastSolve is { } last ? TimeFormat.Format(last.Time) : "0.00";
                    }
                }
                break;
            case TimerState.Stopped:
                State = TimerState.Idle;
                break;
        }
    }

    /// <summary>Escape: abandon inspection or the current run without recording anything.</summary>
    public void Cancel()
    {
        if (State is TimerState.Idle or TimerState.Stopped)
        {
            return;
        }
        _stopwatch.Reset();
        _inspection.Reset();
        _hold.Reset();
        State = TimerState.Idle;
        Visual = TimerVisual.Normal;
        Display = LastSolve is { } last ? TimeFormat.Format(last.Time) : "0.00";
    }

    private void BeginHold()
    {
        _hold.Restart();
        State = TimerState.Holding;
        Visual = _holdStartsRun && _settings.HoldToStartMs > 0 ? TimerVisual.HoldWarming : TimerVisual.HoldReady;
        if (_holdStartsRun)
        {
            Display = "0.00";
        }
    }

    private void StartInspection()
    {
        _inspection.Restart();
        _inspectionPenalty = Penalty.None;
        State = TimerState.Inspecting;
        Visual = TimerVisual.Inspection;
        Tick();
    }

    private void StartRun()
    {
        if (_inspection.IsRunning)
        {
            _inspectionPenalty = PenaltyForInspection(_inspection.Elapsed.TotalSeconds);
            _inspection.Reset();
        }
        _stopwatch.Restart();
        State = TimerState.Running;
        Visual = TimerVisual.Running;
        Display = "0.00";
    }

    private static Penalty PenaltyForInspection(double seconds) => seconds switch
    {
        >= DnfAtSeconds => Penalty.Dnf,
        >= Plus2AtSeconds => Penalty.Plus2,
        _ => Penalty.None,
    };

    private void Stop()
    {
        _stopwatch.Stop();
        var ms = (int)_stopwatch.ElapsedMilliseconds;
        State = TimerState.Stopped;
        Visual = TimerVisual.Result;
        var scramble = CurrentScramble;
        var record = new SolveRecord(Ids.New(), DateTimeOffset.Now, ms, _inspectionPenalty, scramble?.Algorithm.ToString() ?? string.Empty);
        Display = TimeFormat.Format(record.Time);
        _ = SaveSolveAsync(record);
        _ = NewScrambleAsync();
    }

    private async Task SaveSolveAsync(SolveRecord record)
    {
        if (_session is null)
        {
            await RefreshSessionAsync();
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.AddSolveAsync(_session!.Id, record);
    }

    /// <summary>Called by the view about 30 times per second while timing.</summary>
    public void Tick()
    {
        switch (State)
        {
            case TimerState.Holding when _holdStartsRun && _settings.HoldToStartMs > 0:
                Visual = _hold.ElapsedMilliseconds >= _settings.HoldToStartMs ? TimerVisual.HoldReady : TimerVisual.HoldWarming;
                break;
            case TimerState.Holding when !_holdStartsRun:
                break;
            case TimerState.Inspecting:
            case TimerState.Holding:
                if (_inspection.IsRunning)
                {
                    var seconds = _inspection.Elapsed.TotalSeconds;
                    var penalty = PenaltyForInspection(seconds);
                    Display = penalty switch
                    {
                        Penalty.Dnf => "DNF",
                        Penalty.Plus2 => "+2",
                        _ => Math.Max(0, InspectionSeconds - (int)Math.Floor(seconds)).ToString(),
                    };
                    if (State == TimerState.Inspecting)
                    {
                        Visual = penalty == Penalty.None ? TimerVisual.Inspection : TimerVisual.InspectionPenalty;
                    }
                }
                break;
            case TimerState.Running:
                Display = TimeFormat.Format((int)_stopwatch.ElapsedMilliseconds);
                break;
        }
    }

    [RelayCommand]
    private Task TogglePlus2Async() => SetLastPenaltyAsync(LastSolve?.Penalty == Penalty.Plus2 ? Penalty.None : Penalty.Plus2);

    [RelayCommand]
    private Task ToggleDnfAsync() => SetLastPenaltyAsync(LastSolve?.Penalty == Penalty.Dnf ? Penalty.None : Penalty.Dnf);

    [RelayCommand]
    private Task ClearPenaltyAsync() => SetLastPenaltyAsync(Penalty.None);

    private async Task SetLastPenaltyAsync(Penalty penalty)
    {
        if (LastSolve is not { } last || _session is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.UpdateSolveAsync(_session.Id, last with { Penalty = penalty });
    }

    [RelayCommand]
    private async Task DeleteLastAsync()
    {
        if (LastSolve is not { } last || _session is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.DeleteSolveAsync(_session.Id, last.Id);
    }

    private void UpdateLastSolve()
    {
        HasLastSolve = LastSolve is not null;
        IsLastPlus2 = LastSolve?.Penalty == Penalty.Plus2;
        IsLastDnf = LastSolve?.Penalty == Penalty.Dnf;
        LastSolveText = LastSolve is { } last ? $"Last: {TimeFormat.Format(last.Time)}" : string.Empty;
    }

    private void UpdateStats()
    {
        if (_session is null)
        {
            Stats = [];
            return;
        }
        var summary = SessionStatistics.Summarize(_session.Times);
        SessionSummaryText = summary.Count == 0
            ? "No solves yet"
            : $"{summary.Count} solves · mean {TimeFormat.FormatOrDash(summary.Mean)}" + (summary.DnfCount > 0 ? $" · {summary.DnfCount} DNF" : string.Empty);
        Stats = summary.Stats
            .Select(s => new StatRow(
                WcaStatistics.Label(s.Kind),
                s.HasCurrent ? TimeFormat.Format(s.Current) : "-",
                s.Best is { } best ? TimeFormat.Format(best.Ms) : "-"))
            .ToList();
    }
}
