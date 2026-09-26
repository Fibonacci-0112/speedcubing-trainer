using System.Collections.ObjectModel;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Statistics;
using SpeedcubingTrainer.Services;

namespace SpeedcubingTrainer.ViewModels;

public sealed record SolveRow(SolveRecord Record, int Number, string Time, string Ao5, string Ao12, string Date)
{
    public bool HasComment => !string.IsNullOrWhiteSpace(Record.Comment);
}

public sealed record StatCard(string Label, string Current, string Best);

public sealed partial class SessionsViewModel : ObservableObject
{
    private readonly RepositoryProvider _repositories;
    private bool _suppressSelection;

    public SessionsViewModel(RepositoryProvider repositories)
    {
        _repositories = repositories;
        _ = InitializeAsync();
    }

    public ObservableCollection<SessionInfo> Sessions { get; } = [];

    public ObservableCollection<SolveRow> Solves { get; } = [];

    [ObservableProperty]
    public partial SessionInfo? SelectedSession { get; set; }

    [ObservableProperty]
    public partial Session? Current { get; private set; }

    [ObservableProperty]
    public partial IReadOnlyList<SolveTime> Times { get; private set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<StatCard> Cards { get; private set; } = [];

    [ObservableProperty]
    public partial string SummaryText { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = string.Empty;

    public bool HasSolves => Solves.Count > 0;

    private async Task InitializeAsync()
    {
        var repo = await _repositories.GetSessionsAsync();
        repo.Changed += () => _ = ReloadAsync();
        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        var repo = await _repositories.GetSessionsAsync();
        var list = await repo.ListAsync();
        var active = await repo.GetActiveAsync();
        _suppressSelection = true;
        Sessions.Clear();
        foreach (var info in list)
        {
            Sessions.Add(info);
        }
        SelectedSession = Sessions.FirstOrDefault(s => s.Id == active.Id);
        _suppressSelection = false;
        await ShowSessionAsync(active);
    }

    partial void OnSelectedSessionChanged(SessionInfo? value)
    {
        if (_suppressSelection || value is null)
        {
            return;
        }
        _ = SwitchAsync(value.Id);
    }

    private async Task SwitchAsync(string id)
    {
        var repo = await _repositories.GetSessionsAsync();
        if ((await repo.GetActiveAsync()).Id != id)
        {
            await repo.SetActiveAsync(id);
        }
    }

    private Task ShowSessionAsync(Session session)
    {
        Current = session;
        Times = session.Times;
        var ao5 = SessionStatistics.Series(Times, StatKind.Ao5);
        var ao12 = SessionStatistics.Series(Times, StatKind.Ao12);
        Solves.Clear();
        for (var i = session.Solves.Count - 1; i >= 0; i--)
        {
            var record = session.Solves[i];
            Solves.Add(new SolveRow(
                record,
                i + 1,
                TimeFormat.Format(record.Time),
                i >= 4 ? TimeFormat.Format(ao5[i]) : "-",
                i >= 11 ? TimeFormat.Format(ao12[i]) : "-",
                record.At.LocalDateTime.ToString("g")));
        }
        var summary = SessionStatistics.Summarize(Times);
        SummaryText = summary.Count == 0
            ? "No solves in this session yet."
            : $"{summary.Count} solves · mean {TimeFormat.FormatOrDash(summary.Mean)} · best {TimeFormat.FormatOrDash(summary.BestSingle)} · worst {TimeFormat.FormatOrDash(summary.WorstSingle)}"
              + (summary.DnfCount > 0 ? $" · {summary.DnfCount} DNF" : string.Empty);
        Cards = summary.Stats
            .Select(s => new StatCard(WcaStatistics.Label(s.Kind), s.HasCurrent ? TimeFormat.Format(s.Current) : "-", s.Best is { } b ? TimeFormat.Format(b.Ms) : "-"))
            .ToList();
        OnPropertyChanged(nameof(HasSolves));
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreateSessionAsync(string? name)
    {
        var repo = await _repositories.GetSessionsAsync();
        var session = await repo.CreateAsync(string.IsNullOrWhiteSpace(name) ? $"Session {Sessions.Count + 1}" : name);
        await repo.SetActiveAsync(session.Id);
    }

    public async Task RenameCurrentAsync(string name)
    {
        if (Current is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.RenameAsync(Current.Id, name);
    }

    public async Task DeleteCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.DeleteSessionAsync(Current.Id);
        if ((await repo.ListAsync()).Count == 0)
        {
            await repo.GetActiveAsync();
        }
    }

    public async Task ClearCurrentAsync()
    {
        if (Current is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.ClearSolvesAsync(Current.Id);
    }

    public async Task UpdateSolveAsync(SolveRecord record)
    {
        if (Current is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.UpdateSolveAsync(Current.Id, record);
    }

    public async Task DeleteSolveAsync(SolveRecord record)
    {
        if (Current is null)
        {
            return;
        }
        var repo = await _repositories.GetSessionsAsync();
        await repo.DeleteSolveAsync(Current.Id, record.Id);
    }

    public async Task<string> ExportJsonAsync()
    {
        var repo = await _repositories.GetSessionsAsync();
        return await repo.ExportJsonAsync();
    }

    public string ExportCsv() => Current is null ? string.Empty : CsvExport.Session(Current);

    public async Task<int> ImportJsonAsync(string json, ImportMode mode)
    {
        var repo = await _repositories.GetSessionsAsync();
        var count = await repo.ImportJsonAsync(json, mode);
        (await _repositories.GetProgressAsync()).Invalidate();
        StatusMessage = $"Imported {count} session(s).";
        return count;
    }
}
