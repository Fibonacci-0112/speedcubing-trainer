using Microsoft.UI.Xaml.Controls.Primitives;
using SpeedcubingTrainer.Controls;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.ViewModels;

namespace SpeedcubingTrainer.Views;

public sealed partial class AlgorithmsPage : Page
{
    public AlgorithmsPage()
    {
        ViewModel = App.Services.GetRequiredService<AlgorithmsViewModel>();
        this.InitializeComponent();
        Loaded += (_, _) => _ = ViewModel.ReloadAsync();
    }

    public AlgorithmsViewModel ViewModel { get; }

    private async void OnCaseClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CaseCardViewModel card)
        {
            return;
        }
        await ShowCaseAsync(card);
    }

    private async Task ShowCaseAsync(CaseCardViewModel card)
    {
        var algCase = card.Case;
        var content = new StackPanel { Spacing = 10, MinWidth = 320 };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        header.Children.Add(new CaseView { Case = algCase, Size = 140 });
        var info = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        info.Children.Add(new TextBlock { Text = algCase.Group, Opacity = 0.7 });
        if (algCase.Probability is { } p)
        {
            info.Children.Add(new TextBlock { Text = $"Probability {p}", Opacity = 0.7 });
        }
        if (algCase.Recognition is { } r)
        {
            info.Children.Add(new TextBlock { Text = r, TextWrapping = TextWrapping.Wrap, MaxWidth = 220 });
        }
        info.Children.Add(new TextBlock { Text = card.DrillSummary, Opacity = 0.7, FontSize = 12 });
        header.Children.Add(info);
        content.Children.Add(header);

        content.Children.Add(new TextBlock { Text = "Algorithms (pick the one you use)", Style = (Style)Application.Current.Resources["TitleSmall"] });
        for (var i = 0; i < algCase.Algorithms.Count; i++)
        {
            var index = i;
            var radio = new RadioButton
            {
                Content = new TextBlock { Text = algCase.Algorithms[i].ToString(ViewModel.Notation), TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true },
                IsChecked = card.Progress.PreferredAlgorithm == i,
                GroupName = "alg-" + algCase.Id,
            };
            radio.Checked += async (_, _) => await ViewModel.SetPreferredAlgorithmAsync(card, index);
            content.Children.Add(radio);
        }

        var statusBox = new ComboBox
        {
            Header = "Status",
            ItemsSource = new[] { "Not started", "Learning", "Learned" },
            SelectedIndex = (int)card.Status,
            MinWidth = 160,
        };
        statusBox.SelectionChanged += async (_, _) =>
        {
            if (statusBox.SelectedIndex >= 0 && (LearningStatus)statusBox.SelectedIndex != card.Status)
            {
                await ViewModel.SetStatusAsync(card, (LearningStatus)statusBox.SelectedIndex);
            }
        };
        var favorite = new ToggleButton { Content = "★ Favorite", IsChecked = card.IsFavorite, VerticalAlignment = VerticalAlignment.Bottom };
        favorite.Click += async (_, _) => await ViewModel.ToggleFavoriteAsync(card);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(statusBox);
        row.Children.Add(favorite);
        content.Children.Add(row);

        var setupText = new TextBlock { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontSize = 15 };
        var setupPreview = new CaseView { Case = algCase, Size = 96, Visibility = Visibility.Collapsed };
        var setupButton = new Button { Content = "Setup scramble" };
        setupButton.Click += (_, _) =>
        {
            var setup = ViewModel.CreateSetup(algCase);
            setupText.Text = $"Apply to a solved cube: {setup.Setup.ToString(ViewModel.Notation)}";
            setupPreview.State = setup.State;
            setupPreview.Visibility = Visibility.Visible;
        };
        var mirrorText = new TextBlock
        {
            Text = $"Mirror: {algCase.Primary.Mirror().ToString(ViewModel.Notation)}\nInverse: {algCase.Primary.Inverse().ToString(ViewModel.Notation)}",
            Opacity = 0.7,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        content.Children.Add(setupButton);
        var setupRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        setupRow.Children.Add(setupPreview);
        setupRow.Children.Add(setupText);
        content.Children.Add(setupRow);
        content.Children.Add(mirrorText);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = algCase.Title,
            Content = new ScrollViewer { Content = content, MaxHeight = 560 },
            CloseButtonText = "Close",
        };
        await dialog.ShowAsync();
    }
}
