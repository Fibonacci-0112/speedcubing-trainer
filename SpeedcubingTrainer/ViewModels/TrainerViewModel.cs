using System.Collections.ObjectModel;
using System.Diagnostics;
using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Training;
using SpeedcubingTrainer.Services;

namespace SpeedcubingTrainer.ViewModels;

public enum TrainerMode
{
    Drill,
    Memorize,
    Quiz,
}

public enum PoolFilter
{
    Learning,
    LearningAndLearned,
    Favorites,
    Weakest,
    All,
}

public enum DrillPhase
{
    Ready,
    Recognizing,
    Executing,
    Result,
}

public sealed record QuizChoice(string Text, bool IsCorrect);

public sealed record PoolFilterItem(PoolFilter Filter, string Name)
{
    public override string ToString() => Name;
}

public sealed record TrainerModeItem(TrainerMode Mode, string Name)
{
    public override string ToString() => Name;
}

public sealed partial class TrainerViewModel : ObservableObject
{
    private readonly RepositoryProvider _repositories;
    private readonly SettingsService _settings;
    private readonly CaseSetupGenerator _setups = new(SystemRandom.Instance);
    private readonly Sm2Scheduler _scheduler = new();
    private readonly Stopwatch _recognition = new();
    private readonly Stopwatch _execution = new();
    private AlgorithmProgressFile? _progress;
    private List<AlgCase> _pool = [];
    private List<ReviewQueue.Entry> _queue = [];
    private int _queueIndex;
    private int _recognitionMs;

    public TrainerViewModel(RepositoryProvider repositories, SettingsService settings)
    {
        _repositories = repositories;
        _settings = settings;
        _ = ReloadAsync();
    }

    public IReadOnlyList<TrainerModeItem> Modes { get; } =
    [
        new(TrainerMode.Drill, "Drill"),
        new(TrainerMode.Memorize, "Memorize"),
        new(TrainerMode.Quiz, "Recognition quiz"),
    ];

    public IReadOnlyList<PoolFilterItem> PoolFilters { get; } =
    [
        new(PoolFilter.Learning, "Learning"),
        new(PoolFilter.LearningAndLearned, "Learning and learned"),
        new(PoolFilter.Favorites, "Favorites"),
        new(PoolFilter.Weakest, "10 slowest"),
        new(PoolFilter.All, "All cases"),
    ];

    public TrainerModeItem SelectedMode
    {
        get => Modes.First(m => m.Mode == Mode);
        set => Mode = value?.Mode ?? TrainerMode.Drill;
    }

    public PoolFilterItem SelectedPool
    {
        get => PoolFilters.First(p => p.Filter == Pool);
        set => Pool = value?.Filter ?? PoolFilter.LearningAndLearned;
    }

    [ObservableProperty]
    public partial TrainerMode Mode { get; set; } = TrainerMode.Drill;

    [ObservableProperty]
    public partial bool IncludeF2L { get; set; }

    [ObservableProperty]
    public partial bool IncludeOll { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludePll { get; set; } = true;

    [ObservableProperty]
    public partial PoolFilter Pool { get; set; } = PoolFilter.LearningAndLearned;

    [ObservableProperty]
    public partial string PoolText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial AlgCase? CurrentCase { get; private set; }

    [ObservableProperty]
    public partial CubeState? CurrentState { get; private set; }

    [ObservableProperty]
    public partial string SetupText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AnswerText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAnswerVisible { get; private set; }

    [ObservableProperty]
    public partial string StatusText { get; private set; } = "Choose sets and press Start.";

    // Drill
    [ObservableProperty]
    public partial DrillPhase Phase { get; private set; } = DrillPhase.Ready;

    [ObservableProperty]
    public partial string DrillTimeText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string DrillHint { get; private set; } = "Press Start, then space (or tap) when you recognise the case, and again when you finish executing.";

    // Memorize
    [ObservableProperty]
    public partial string QueueText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AgainText { get; private set; } = "Again";

    [ObservableProperty]
    public partial string HardText { get; private set; } = "Hard";

    [ObservableProperty]
    public partial string GoodText { get; private set; } = "Good";

    [ObservableProperty]
    public partial string EasyText { get; private set; } = "Easy";

    // Quiz
    public ObservableCollection<QuizChoice> Choices { get; } = [];

    [ObservableProperty]
    public partial string QuizFeedback { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial int QuizAsked { get; private set; }

    [ObservableProperty]
    public partial int QuizCorrect { get; private set; }

    public bool IsDrill => Mode == TrainerMode.Drill;

    public bool IsMemorize => Mode == TrainerMode.Memorize;

    public bool IsQuiz => Mode == TrainerMode.Quiz;

    public bool HasCase => CurrentCase is not null;

    public string ScoreText => QuizAsked == 0 ? string.Empty : $"{QuizCorrect}/{QuizAsked} correct";

    partial void OnModeChanged(TrainerMode value)
    {
        OnPropertyChanged(nameof(SelectedMode));
        OnPropertyChanged(nameof(IsDrill));
        OnPropertyChanged(nameof(IsMemorize));
        OnPropertyChanged(nameof(IsQuiz));
        ResetRound();
        _ = ReloadAsync();
    }

    partial void OnIncludeF2LChanged(bool value) => _ = ReloadAsync();

    partial void OnIncludeOllChanged(bool value) => _ = ReloadAsync();

    partial void OnIncludePllChanged(bool value) => _ = ReloadAsync();

    partial void OnPoolChanged(PoolFilter value)
    {
        OnPropertyChanged(nameof(SelectedPool));
        _ = ReloadAsync();
    }

    partial void OnCurrentCaseChanged(AlgCase? value) => OnPropertyChanged(nameof(HasCase));

    public async Task ReloadAsync()
    {
        var repo = await _repositories.GetProgressAsync();
        _progress = await repo.LoadAsync();
        var sets = new List<AlgorithmSet>();
        if (IncludeF2L)
        {
            sets.Add(AlgorithmSetLoader.Load(AlgorithmSetKind.F2L));
        }
        if (IncludeOll)
        {
            sets.Add(AlgorithmSetLoader.Load(AlgorithmSetKind.OLL));
        }
        if (IncludePll)
        {
            sets.Add(AlgorithmSetLoader.Load(AlgorithmSetKind.PLL));
        }
        var all = sets.SelectMany(s => s.Cases).ToList();
        _pool = all.Where(c => Pool switch
        {
            PoolFilter.Learning => Record(c).Status == LearningStatus.Learning,
            PoolFilter.LearningAndLearned => Record(c).Status != LearningStatus.NotStarted,
            PoolFilter.Favorites => Record(c).Favorite,
            PoolFilter.Weakest => true,
            _ => true,
        }).ToList();
        if (Pool == PoolFilter.Weakest)
        {
            _pool = _pool
                .Where(c => Record(c).Status != LearningStatus.NotStarted)
                .OrderByDescending(c => DrillStats.MeanTotalMs(Record(c)) ?? double.MaxValue)
                .Take(10)
                .ToList();
        }

        if (Mode == TrainerMode.Memorize)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            _queue = ReviewQueue.Build(all, _progress.Cases, today, _settings.NewCasesPerDay,
                (c, p) => Pool switch
                {
                    PoolFilter.Learning => p.Status == LearningStatus.Learning,
                    PoolFilter.LearningAndLearned => p.Status != LearningStatus.NotStarted,
                    PoolFilter.Favorites => p.Favorite,
                    PoolFilter.Weakest => p.Status != LearningStatus.NotStarted,
                    _ => true,
                }).ToList();
            _queueIndex = 0;
            var due = _queue.Count(e => !e.IsNew);
            QueueText = _queue.Count == 0 ? "Nothing to review right now." : $"{due} due · {_queue.Count - due} new";
            PoolText = $"{_queue.Count} cards in today's queue";
        }
        else
        {
            PoolText = _pool.Count == 0
                ? "No cases match. Mark cases as Learning or Learned on the Algorithms page, or widen the filter."
                : $"{_pool.Count} cases in the pool";
        }
    }

    private CaseProgressRecord Record(AlgCase c)
    {
        if (_progress is null)
        {
            return new CaseProgressRecord();
        }
        if (!_progress.Cases.TryGetValue(c.Id, out var record))
        {
            record = new CaseProgressRecord();
            _progress.Cases[c.Id] = record;
        }
        return record;
    }

    private void ResetRound()
    {
        _recognition.Reset();
        _execution.Reset();
        Phase = DrillPhase.Ready;
        CurrentCase = null;
        CurrentState = null;
        SetupText = string.Empty;
        AnswerText = string.Empty;
        IsAnswerVisible = false;
        DrillTimeText = string.Empty;
        QuizFeedback = string.Empty;
        Choices.Clear();
        StatusText = Mode switch
        {
            TrainerMode.Drill => "Choose sets and press Start.",
            TrainerMode.Memorize => "Review the cases that are due today.",
            _ => "Press Next question to begin.",
        };
    }

    private void Present(AlgCase c)
    {
        var set = AlgorithmSetLoader.Load(c.Set);
        var setup = _setups.Create(c, set.SetupDefaults, Record(c).PreferredAlgorithm);
        CurrentCase = c;
        CurrentState = setup.State;
        SetupText = setup.Setup.ToString(_settings.NotationStyle);
        AnswerText = c.Algorithms[Math.Clamp(Record(c).PreferredAlgorithm, 0, c.Algorithms.Count - 1)].ToString(_settings.NotationStyle);
        IsAnswerVisible = false;
    }

    // ---- Drill ----

    [RelayCommand]
    public void StartDrill()
    {
        if (_pool.Count == 0)
        {
            StatusText = "The pool is empty.";
            return;
        }
        var next = _pool[Random.Shared.Next(_pool.Count)];
        if (_pool.Count > 1 && next == CurrentCase)
        {
            next = _pool[(_pool.IndexOf(next) + 1) % _pool.Count];
        }
        Present(next);
        Phase = DrillPhase.Recognizing;
        _recognition.Restart();
        _execution.Reset();
        DrillTimeText = "0.00";
        StatusText = "Recognise the case…";
    }

    /// <summary>Space bar or tap during a drill: advances recognition → execution → result.</summary>
    public void DrillAdvance()
    {
        switch (Phase)
        {
            case DrillPhase.Ready:
            case DrillPhase.Result:
                StartDrill();
                break;
            case DrillPhase.Recognizing:
                _recognition.Stop();
                _recognitionMs = (int)_recognition.ElapsedMilliseconds;
                _execution.Restart();
                Phase = DrillPhase.Executing;
                IsAnswerVisible = true;
                StatusText = $"Recognised in {_recognitionMs / 1000.0:0.00} s. Execute!";
                break;
            case DrillPhase.Executing:
                _execution.Stop();
                _ = FinishDrillAsync(success: true);
                break;
        }
    }

    [RelayCommand]
    private Task FailDrillAsync() => Phase is DrillPhase.Recognizing or DrillPhase.Executing ? FinishDrillAsync(success: false) : Task.CompletedTask;

    private async Task FinishDrillAsync(bool success)
    {
        if (CurrentCase is not { } c)
        {
            return;
        }
        _recognition.Stop();
        _execution.Stop();
        if (Phase == DrillPhase.Recognizing)
        {
            _recognitionMs = (int)_recognition.ElapsedMilliseconds;
        }
        var executionMs = (int)_execution.ElapsedMilliseconds;
        Phase = DrillPhase.Result;
        IsAnswerVisible = true;
        var record = Record(c);
        DrillStats.Record(record, new DrillAttemptRecord(DateTimeOffset.Now, _recognitionMs, executionMs, success));
        var repo = await _repositories.GetProgressAsync();
        await repo.SaveAsync(c.Id, record);
        var total = _recognitionMs + executionMs;
        DrillTimeText = success ? $"{total / 1000.0:0.00}" : "✗";
        var mean = DrillStats.MeanTotalMs(record);
        StatusText = success
            ? $"Recognition {_recognitionMs / 1000.0:0.00} s + execution {executionMs / 1000.0:0.00} s = {total / 1000.0:0.00} s" + (mean is { } m ? $" · mean {m / 1000:0.00} s" : string.Empty)
            : "Marked as failed. Press Start for the next case.";
    }

    public void DrillTick()
    {
        if (Phase == DrillPhase.Recognizing)
        {
            DrillTimeText = $"{_recognition.Elapsed.TotalSeconds:0.00}";
        }
        else if (Phase == DrillPhase.Executing)
        {
            DrillTimeText = $"{(_recognition.Elapsed + _execution.Elapsed).TotalSeconds:0.00}";
        }
    }

    // ---- Memorize ----

    [RelayCommand]
    public void StartMemorize()
    {
        if (_queueIndex >= _queue.Count)
        {
            _ = ReloadAsync().ContinueWith(_ => ShowNextCard(), TaskScheduler.FromCurrentSynchronizationContext());
            return;
        }
        ShowNextCard();
    }

    private void ShowNextCard()
    {
        if (_queueIndex >= _queue.Count)
        {
            ResetRound();
            StatusText = "All done for today. Come back tomorrow, or add more cases as Learning.";
            return;
        }
        var entry = _queue[_queueIndex];
        Present(entry.Case);
        var state = ReviewState.From(entry.Progress);
        var preview = _scheduler.PreviewIntervals(state);
        AgainText = $"Again\n{Describe(preview[Grade.Again])}";
        HardText = $"Hard\n{Describe(preview[Grade.Hard])}";
        GoodText = $"Good\n{Describe(preview[Grade.Good])}";
        EasyText = $"Easy\n{Describe(preview[Grade.Easy])}";
        StatusText = entry.IsNew ? "New case. Recall the algorithm, then reveal it." : "Recall the algorithm, then reveal it.";
        QueueText = $"{_queueIndex + 1} / {_queue.Count}";
    }

    private static string Describe(int days) => days switch
    {
        0 => "today",
        1 => "1 day",
        < 30 => $"{days} days",
        < 365 => $"{Math.Round(days / 30.0, 1)} mo",
        _ => $"{Math.Round(days / 365.0, 1)} yr",
    };

    [RelayCommand]
    public void RevealAnswer()
    {
        if (CurrentCase is not null)
        {
            IsAnswerVisible = true;
        }
    }

    [RelayCommand]
    private async Task GradeAsync(string gradeName)
    {
        if (CurrentCase is not { } c || !Enum.TryParse<Grade>(gradeName, out var grade))
        {
            return;
        }
        var record = Record(c);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var next = _scheduler.Review(ReviewState.From(record), grade, today);
        next.ApplyTo(record);
        record.LastReviewed = DateTimeOffset.Now;
        if (record.Status == LearningStatus.NotStarted)
        {
            record.Status = LearningStatus.Learning;
        }
        var repo = await _repositories.GetProgressAsync();
        await repo.SaveAsync(c.Id, record);
        if (grade == Grade.Again)
        {
            // Failed cards come back at the end of today's queue.
            _queue.Add(_queue[_queueIndex] with { Progress = record, IsNew = false });
        }
        _queueIndex++;
        ShowNextCard();
    }

    // ---- Quiz ----

    [RelayCommand]
    public void StartQuiz()
    {
        if (_pool.Count < 2)
        {
            StatusText = "The quiz needs at least two cases in the pool.";
            return;
        }
        var correct = _pool[Random.Shared.Next(_pool.Count)];
        Present(correct);
        var sameSet = AlgorithmSetLoader.Load(correct.Set).Cases.Where(c => c != correct).OrderBy(_ => Random.Shared.Next()).Take(3);
        var options = sameSet.Select(c => new QuizChoice(c.Title, false)).Append(new QuizChoice(correct.Title, true)).OrderBy(_ => Random.Shared.Next()).ToList();
        Choices.Clear();
        foreach (var o in options)
        {
            Choices.Add(o);
        }
        QuizFeedback = string.Empty;
        StatusText = "Which case is this?";
    }

    [RelayCommand]
    private async Task AnswerQuizAsync(QuizChoice choice)
    {
        if (CurrentCase is not { } c || QuizFeedback.Length > 0)
        {
            return;
        }
        QuizAsked++;
        var record = Record(c);
        record.QuizAsked++;
        if (choice.IsCorrect)
        {
            QuizCorrect++;
            record.QuizCorrect++;
            QuizFeedback = "Correct!";
        }
        else
        {
            QuizFeedback = $"No, this is {c.Title}.";
        }
        IsAnswerVisible = true;
        OnPropertyChanged(nameof(ScoreText));
        var repo = await _repositories.GetProgressAsync();
        await repo.SaveAsync(c.Id, record);
    }
}
