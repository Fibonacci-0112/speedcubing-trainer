using SpeedcubingTrainer.Views;

namespace SpeedcubingTrainer;

public sealed partial class Shell : Page
{
    private static readonly Dictionary<string, Type> Pages = new()
    {
        ["timer"] = typeof(TimerPage),
        ["sessions"] = typeof(SessionsPage),
        ["algorithms"] = typeof(AlgorithmsPage),
        ["trainer"] = typeof(TrainerPage),
        ["settings"] = typeof(SettingsPage),
    };

    public Shell()
    {
        this.InitializeComponent();
        Loaded += (_, _) => Nav.SelectedItem = Nav.MenuItems[0];
    }

    private void OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = args.IsSettingsSelected
            ? "settings"
            : (args.SelectedItemContainer as NavigationViewItem)?.Tag as string;

        if (tag is not null && Pages.TryGetValue(tag, out var pageType) && ContentFrame.CurrentSourcePageType != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }
}
