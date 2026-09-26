using Microsoft.UI.Xaml.Input;
using SpeedcubingTrainer.ViewModels;
using Windows.System;
using Windows.UI;

namespace SpeedcubingTrainer.Views;

public sealed partial class TimerPage : Page
{
    private static readonly SolidColorBrush NormalBrush = new(Color.FromArgb(255, 235, 235, 235));
    private static readonly SolidColorBrush WarmBrush = new(Color.FromArgb(255, 235, 80, 60));
    private static readonly SolidColorBrush ReadyBrush = new(Color.FromArgb(255, 60, 200, 90));
    private static readonly SolidColorBrush InspectionBrush = new(Color.FromArgb(255, 255, 200, 40));
    private static readonly SolidColorBrush PenaltyBrush = new(Color.FromArgb(255, 235, 80, 60));
    private static readonly SolidColorBrush RunningBrush = new(Color.FromArgb(255, 235, 235, 235));
    private static readonly SolidColorBrush ResultBrush = new(Color.FromArgb(255, 120, 200, 255));

    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private bool _spaceDown;
    private bool _pointerDown;

    public TimerPage()
    {
        ViewModel = App.Services.GetRequiredService<TimerViewModel>();
        this.InitializeComponent();
        // Key events only bubble from a focused element, so the page itself must be able to take focus.
        IsTabStop = true;
        UseSystemFocusVisuals = false;
        _tick.Tick += (_, _) => ViewModel.Tick();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public TimerViewModel ViewModel { get; }

    public static Brush BrushFor(TimerVisual visual) => visual switch
    {
        TimerVisual.HoldWarming => WarmBrush,
        TimerVisual.HoldReady => ReadyBrush,
        TimerVisual.Inspection => InspectionBrush,
        TimerVisual.InspectionPenalty => PenaltyBrush,
        TimerVisual.Running => RunningBrush,
        TimerVisual.Result => ResultBrush,
        _ => NormalBrush,
    };

    public static Visibility Collapse(bool hide) => hide ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility Show(bool show) => show ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility ShowIf(bool show, bool unless) => show && !unless ? Visibility.Visible : Visibility.Collapsed;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _tick.Start();
        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown += OnKeyDown;
            root.KeyUp += OnKeyUp;
        }
        TakeFocus();
    }

    private void TakeFocus()
    {
        if (!IsTextInputFocused())
        {
            Focus(FocusState.Programmatic);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _tick.Stop();
        if (XamlRoot?.Content is UIElement root)
        {
            root.KeyDown -= OnKeyDown;
            root.KeyUp -= OnKeyUp;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            ViewModel.Cancel();
            e.Handled = true;
            return;
        }
        if (IsTextInputFocused())
        {
            return;
        }
        if (e.Key == VirtualKey.Space)
        {
            if (!_spaceDown)
            {
                _spaceDown = true;
                ViewModel.Press();
            }
            e.Handled = true;
        }
        else if (ViewModel.State == TimerState.Running)
        {
            // Any key stops the timer, like most desktop timers.
            _spaceDown = true;
            ViewModel.Press();
            e.Handled = true;
        }
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs e)
    {
        if (_spaceDown && (e.Key == VirtualKey.Space || ViewModel.State == TimerState.Stopped))
        {
            _spaceDown = false;
            ViewModel.Release();
            e.Handled = true;
        }
    }

    private void OnSurfacePressed(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerDown)
        {
            return;
        }
        _pointerDown = true;
        TakeFocus();
        TimerSurface.CapturePointer(e.Pointer);
        ViewModel.Press();
        e.Handled = true;
    }

    private void OnSurfaceReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pointerDown)
        {
            return;
        }
        _pointerDown = false;
        TimerSurface.ReleasePointerCaptures();
        ViewModel.Release();
        e.Handled = true;
    }

    private bool IsTextInputFocused() =>
        XamlRoot is not null && FocusManager.GetFocusedElement(XamlRoot) is TextBox or PasswordBox or RichEditBox;
}
