using Microsoft.UI.Xaml.Input;
using SpeedcubingTrainer.ViewModels;
using Windows.System;

namespace SpeedcubingTrainer.Views;

public sealed partial class TrainerPage : Page
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(50) };

    public TrainerPage()
    {
        ViewModel = App.Services.GetRequiredService<TrainerViewModel>();
        this.InitializeComponent();
        IsTabStop = true;
        UseSystemFocusVisuals = false;
        _tick.Tick += (_, _) => ViewModel.DrillTick();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public TrainerViewModel ViewModel { get; }

    public static Visibility Show(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility Collapse(bool hide) => hide ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility ShowIf(bool show, bool unless) => show && !unless ? Visibility.Visible : Visibility.Collapsed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tick.Start();
        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown += OnKeyDown;
        }
        _ = ViewModel.ReloadAsync();
        Focus(FocusState.Programmatic);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown -= OnKeyDown;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Space || XamlRoot is null || FocusManager.GetFocusedElement(XamlRoot) is TextBox)
        {
            return;
        }
        Advance();
        e.Handled = true;
    }

    private void OnSurfacePressed(object sender, PointerRoutedEventArgs e)
    {
        Focus(FocusState.Programmatic);
        if (ViewModel.Mode == TrainerMode.Drill)
        {
            Advance();
            e.Handled = true;
        }
    }

    private void Advance()
    {
        switch (ViewModel.Mode)
        {
            case TrainerMode.Drill:
                ViewModel.DrillAdvance();
                break;
            case TrainerMode.Memorize:
                if (ViewModel.HasCase)
                {
                    ViewModel.RevealAnswer();
                }
                else
                {
                    ViewModel.StartMemorize();
                }
                break;
            case TrainerMode.Quiz:
                ViewModel.StartQuiz();
                break;
        }
    }

    private async void OnChoiceClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is QuizChoice choice)
        {
            await ViewModel.AnswerQuizCommand.ExecuteAsync(choice);
        }
    }
}
