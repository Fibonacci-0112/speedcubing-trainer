using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;
using Windows.Storage;

namespace SpeedcubingTrainer.Services;

/// <summary>User preferences, stored in the platform's local settings container.</summary>
public sealed partial class SettingsService : ObservableObject
{
    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public SettingsService()
    {
        InspectionEnabled = Get(nameof(InspectionEnabled), true);
        HoldToStartMs = Get(nameof(HoldToStartMs), 300);
        ScrambleKind = Enum.TryParse<ScrambleKind>(Get(nameof(ScrambleKind), "RandomState"), out var kind) ? kind : ScrambleKind.RandomState;
        TimerFontSize = Get(nameof(TimerFontSize), 96.0);
        NotationStyle = Enum.TryParse<NotationStyle>(Get(nameof(NotationStyle), "Lowercase"), out var style) ? style : NotationStyle.Lowercase;
        ConfirmDelete = Get(nameof(ConfirmDelete), true);
        ShowScramblePreview = Get(nameof(ShowScramblePreview), true);
        NewCasesPerDay = Get(nameof(NewCasesPerDay), 5);
    }

    [ObservableProperty]
    public partial bool InspectionEnabled { get; set; }

    /// <summary>How long the timer must be held before releasing starts it. 0 starts on release immediately.</summary>
    [ObservableProperty]
    public partial int HoldToStartMs { get; set; }

    [ObservableProperty]
    public partial ScrambleKind ScrambleKind { get; set; }

    [ObservableProperty]
    public partial double TimerFontSize { get; set; }

    [ObservableProperty]
    public partial NotationStyle NotationStyle { get; set; }

    [ObservableProperty]
    public partial bool ConfirmDelete { get; set; }

    [ObservableProperty]
    public partial bool ShowScramblePreview { get; set; }

    [ObservableProperty]
    public partial int NewCasesPerDay { get; set; }

    partial void OnInspectionEnabledChanged(bool value) => Set(nameof(InspectionEnabled), value);

    partial void OnHoldToStartMsChanged(int value) => Set(nameof(HoldToStartMs), value);

    partial void OnScrambleKindChanged(ScrambleKind value) => Set(nameof(ScrambleKind), value.ToString());

    partial void OnTimerFontSizeChanged(double value) => Set(nameof(TimerFontSize), value);

    partial void OnNotationStyleChanged(NotationStyle value) => Set(nameof(NotationStyle), value.ToString());

    partial void OnConfirmDeleteChanged(bool value) => Set(nameof(ConfirmDelete), value);

    partial void OnShowScramblePreviewChanged(bool value) => Set(nameof(ShowScramblePreview), value);

    partial void OnNewCasesPerDayChanged(int value) => Set(nameof(NewCasesPerDay), value);

    private T Get<T>(string key, T fallback)
    {
        try
        {
            return _settings.Values.TryGetValue(key, out var value) && value is T typed ? typed : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    private void Set<T>(string key, T value)
    {
        try
        {
            _settings.Values[key] = value;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Could not persist setting {key}: {ex.Message}");
        }
    }
}
