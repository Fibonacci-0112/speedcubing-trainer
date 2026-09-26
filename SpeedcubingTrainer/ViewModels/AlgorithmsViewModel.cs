using System.Collections.ObjectModel;
using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Training;
using SpeedcubingTrainer.Services;

namespace SpeedcubingTrainer.ViewModels;

public enum CaseFilter
{
    All,
    NotStarted,
    Learning,
    Learned,
    Favorites,
}

public sealed partial class CaseCardViewModel(AlgCase algCase, CaseProgressRecord progress, NotationStyle style) : ObservableObject
{
    public AlgCase Case { get; } = algCase;

    public CaseProgressRecord Progress { get; private set; } = progress;

    public string Title => Case.Title;

    public string Group => Case.Group;

    public string AlgorithmText => Case.Algorithms[Math.Clamp(Progress.PreferredAlgorithm, 0, Case.Algorithms.Count - 1)].ToString(style);

    public LearningStatus Status => Progress.Status;

    public string StatusText => Status switch
    {
        LearningStatus.Learning => "Learning",
        LearningStatus.Learned => "Learned",
        _ => "Not started",
    };

    public bool IsFavorite => Progress.Favorite;

    /// <summary>Segoe Fluent / Uno Fluent Assets star glyphs (filled and outline).</summary>
    public string FavoriteGlyph => IsFavorite ? "\uE735" : "\uE734";

    public string DrillSummary => Progress.DrillAttempts == 0
        ? "No drills yet"
        : $"{Progress.DrillSuccesses}/{Progress.DrillAttempts} drills · mean {(DrillStats.MeanTotalMs(Progress) is { } m ? (m / 1000).ToString("0.00") + " s" : "-")}";

    public void Update(CaseProgressRecord progress)
    {
        Progress = progress;
        OnPropertyChanged(string.Empty);
    }
}

public sealed class CaseGroupViewModel(string name, IReadOnlyList<CaseCardViewModel> cases)
{
    public string Name { get; } = name;

    public IReadOnlyList<CaseCardViewModel> Cases { get; } = cases;

    public string CountText => $"{Cases.Count} case{(Cases.Count == 1 ? "" : "s")}";
}

public sealed partial class AlgorithmsViewModel : ObservableObject
{
    private readonly RepositoryProvider _repositories;
    private readonly SettingsService _settings;
    private readonly CaseSetupGenerator _setups = new(SystemRandom.Instance);
    private readonly Dictionary<string, CaseCardViewModel> _cards = new();
    private AlgorithmProgressFile? _progress;

    public AlgorithmsViewModel(RepositoryProvider repositories, SettingsService settings)
    {
        _repositories = repositories;
        _settings = settings;
        Sets = AlgorithmSetLoader.LoadAll();
        SelectedSet = Sets.First(s => s.Kind == AlgorithmSetKind.OLL);
        _settings.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsService.NotationStyle))
            {
                _ = ReloadAsync();
            }
        };
        _ = ReloadAsync();
    }

    public IReadOnlyList<AlgorithmSet> Sets { get; }

    public IReadOnlyList<CaseFilter> Filters { get; } = Enum.GetValues<CaseFilter>();

    [ObservableProperty]
    public partial AlgorithmSet SelectedSet { get; set; }

    [ObservableProperty]
    public partial CaseFilter Filter { get; set; } = CaseFilter.All;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    public ObservableCollection<CaseGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = string.Empty;

    public NotationStyle Notation => _settings.NotationStyle;

    partial void OnSelectedSetChanged(AlgorithmSet value) => _ = ReloadAsync();

    partial void OnFilterChanged(CaseFilter value) => Rebuild();

    partial void OnSearchChanged(string value) => Rebuild();

    public async Task ReloadAsync()
    {
        var repo = await _repositories.GetProgressAsync();
        _progress = await repo.LoadAsync();
        _cards.Clear();
        foreach (var c in SelectedSet.Cases)
        {
            _cards[c.Id] = new CaseCardViewModel(c, GetRecord(c.Id), Notation);
        }
        Rebuild();
    }

    private CaseProgressRecord GetRecord(string caseId)
    {
        if (_progress is null)
        {
            return new CaseProgressRecord();
        }
        if (!_progress.Cases.TryGetValue(caseId, out var record))
        {
            record = new CaseProgressRecord();
        }
        return record;
    }

    private void Rebuild()
    {
        Groups.Clear();
        var search = Search.Trim();
        var visible = _cards.Values.Where(Matches).ToList();
        foreach (var groupName in SelectedSet.Groups)
        {
            var cases = visible.Where(c => c.Group == groupName).OrderBy(c => c.Case.Number).ToList();
            if (cases.Count > 0)
            {
                Groups.Add(new CaseGroupViewModel(groupName, cases));
            }
        }
        var all = _cards.Values.ToList();
        var learned = all.Count(c => c.Status == LearningStatus.Learned);
        var learning = all.Count(c => c.Status == LearningStatus.Learning);
        SummaryText = $"{SelectedSet.Name}: {learned} learned, {learning} learning, {all.Count - learned - learning} not started · showing {visible.Count}";

        bool Matches(CaseCardViewModel c)
        {
            var byFilter = Filter switch
            {
                CaseFilter.NotStarted => c.Status == LearningStatus.NotStarted,
                CaseFilter.Learning => c.Status == LearningStatus.Learning,
                CaseFilter.Learned => c.Status == LearningStatus.Learned,
                CaseFilter.Favorites => c.IsFavorite,
                _ => true,
            };
            if (!byFilter)
            {
                return false;
            }
            return search.Length == 0
                   || c.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || c.Group.Contains(search, StringComparison.OrdinalIgnoreCase)
                   || c.Case.Algorithms.Any(a => a.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                   || c.Case.Number.ToString() == search;
        }
    }

    public async Task SetStatusAsync(CaseCardViewModel card, LearningStatus status)
    {
        var record = card.Progress;
        record.Status = status;
        await SaveAsync(card, record);
    }

    public async Task ToggleFavoriteAsync(CaseCardViewModel card)
    {
        var record = card.Progress;
        record.Favorite = !record.Favorite;
        await SaveAsync(card, record);
    }

    public async Task SetPreferredAlgorithmAsync(CaseCardViewModel card, int index)
    {
        var record = card.Progress;
        record.PreferredAlgorithm = index;
        await SaveAsync(card, record);
    }

    private async Task SaveAsync(CaseCardViewModel card, CaseProgressRecord record)
    {
        var repo = await _repositories.GetProgressAsync();
        await repo.SaveAsync(card.Case.Id, record);
        card.Update(record);
        Rebuild();
    }

    public CaseSetup CreateSetup(AlgCase algCase) => _setups.Create(algCase, SelectedSet.SetupDefaults, GetRecord(algCase.Id).PreferredAlgorithm);
}
