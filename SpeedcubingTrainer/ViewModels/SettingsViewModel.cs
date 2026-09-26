using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Services;
using Uno.Extensions;

namespace SpeedcubingTrainer.ViewModels;

public sealed partial class SettingsViewModel(SettingsService settings, ScrambleService scrambles) : ObservableObject
{
    public SettingsService Settings { get; } = settings;

    public ScrambleService Scrambles { get; } = scrambles;

    public bool UseRandomState
    {
        get => Settings.ScrambleKind == ScrambleKind.RandomState;
        set => Settings.ScrambleKind = value ? ScrambleKind.RandomState : ScrambleKind.RandomMoves;
    }

    public bool UseLowercaseWide
    {
        get => Settings.NotationStyle == NotationStyle.Lowercase;
        set => Settings.NotationStyle = value ? NotationStyle.Lowercase : NotationStyle.Wide;
    }

    public double HoldToStartSeconds
    {
        get => Settings.HoldToStartMs / 1000.0;
        set => Settings.HoldToStartMs = (int)Math.Round(Math.Clamp(value, 0, 2) * 1000);
    }

    public double TimerFontSize
    {
        get => Settings.TimerFontSize;
        set => Settings.TimerFontSize = Math.Clamp(value, 40, 200);
    }

    public double NewCasesPerDay
    {
        get => Settings.NewCasesPerDay;
        set => Settings.NewCasesPerDay = (int)Math.Clamp(value, 1, 30);
    }

    public string SolverInfo => Scrambles.IsSolverReady
        ? "Random-state scrambler ready."
        : $"Preparing random-state scrambler… {Scrambles.SolverProgress:P0}";
}
