using SpeedcubingTrainer.ViewModels;
using Uno.Extensions;

namespace SpeedcubingTrainer.Views;

public sealed partial class SettingsPage : Page
{
    private bool _ready;

    public SettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    public SettingsViewModel ViewModel { get; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var themeService = App.Services.GetService<IThemeService>();
        if (themeService is not null)
        {
            ThemeBox.SelectedIndex = themeService.Theme switch
            {
                AppTheme.Light => 1,
                AppTheme.Dark => 2,
                _ => 0,
            };
        }
        await Task.Yield();
        _ready = true;
    }

    private async void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }
        var themeService = App.Services.GetService<IThemeService>();
        if (themeService is null)
        {
            return;
        }
        var theme = ThemeBox.SelectedIndex switch
        {
            1 => AppTheme.Light,
            2 => AppTheme.Dark,
            _ => AppTheme.System,
        };
        await themeService.SetThemeAsync(theme);
    }
}
