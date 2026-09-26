using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Statistics;
using SpeedcubingTrainer.Services;
using SpeedcubingTrainer.ViewModels;

namespace SpeedcubingTrainer.Views;

public sealed partial class SessionsPage : Page
{
    public SessionsPage()
    {
        ViewModel = App.Services.GetRequiredService<SessionsViewModel>();
        this.InitializeComponent();
        Loaded += (_, _) => _ = ViewModel.ReloadAsync();
    }

    public SessionsViewModel ViewModel { get; }

    private async void OnNewSession(object sender, RoutedEventArgs e)
    {
        var name = await Dialogs.PromptAsync(XamlRoot!, "New session", "Name", $"Session {ViewModel.Sessions.Count + 1}");
        if (name is not null)
        {
            await ViewModel.CreateSessionCommand.ExecuteAsync(name);
        }
    }

    private async void OnRenameSession(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Current is null)
        {
            return;
        }
        var name = await Dialogs.PromptAsync(XamlRoot!, "Rename session", "Name", ViewModel.Current.Name);
        if (!string.IsNullOrWhiteSpace(name))
        {
            await ViewModel.RenameCurrentAsync(name);
        }
    }

    private async void OnDeleteSession(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Current is null)
        {
            return;
        }
        if (await Dialogs.ConfirmAsync(XamlRoot!, "Delete session", $"Delete \"{ViewModel.Current.Name}\" and its {ViewModel.Current.Solves.Count} solves?"))
        {
            await ViewModel.DeleteCurrentAsync();
        }
    }

    private async void OnClearSolves(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Current is null || ViewModel.Current.Solves.Count == 0)
        {
            return;
        }
        if (await Dialogs.ConfirmAsync(XamlRoot!, "Clear solves", $"Remove all {ViewModel.Current.Solves.Count} solves from this session?"))
        {
            await ViewModel.ClearCurrentAsync();
        }
    }

    private async void OnExportJson(object sender, RoutedEventArgs e)
    {
        var json = await ViewModel.ExportJsonAsync();
        await FileTransfer.SaveAsync(XamlRoot!, "speedcubing-trainer-export.json", json, "JSON", ".json");
    }

    private async void OnExportCsv(object sender, RoutedEventArgs e)
    {
        var csv = ViewModel.ExportCsv();
        var name = (ViewModel.Current?.Name ?? "session").Replace(' ', '-');
        await FileTransfer.SaveAsync(XamlRoot!, $"{name}.csv", csv, "CSV", ".csv");
    }

    private async void OnImportJson(object sender, RoutedEventArgs e)
    {
        var json = await FileTransfer.OpenTextAsync(XamlRoot!, ".json");
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }
        var replace = await Dialogs.ChooseAsync(XamlRoot!, "Import", "Merge the imported sessions with the existing ones, or replace everything?", "Merge", "Replace all");
        if (replace is null)
        {
            return;
        }
        try
        {
            await ViewModel.ImportJsonAsync(json, replace.Value ? ImportMode.Replace : ImportMode.Merge);
        }
        catch (Exception ex)
        {
            await Dialogs.MessageAsync(XamlRoot!, "Import failed", ex.Message);
        }
    }

    private async void OnSolveClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not SolveRow row)
        {
            return;
        }
        var record = row.Record;
        var scramble = new TextBlock { Text = record.Scramble, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
        var penalty = new ComboBox { ItemsSource = new[] { "OK", "+2", "DNF" }, SelectedIndex = (int)record.Penalty, Header = "Penalty" };
        var comment = new TextBox { Text = record.Comment ?? string.Empty, Header = "Comment", AcceptsReturn = false };
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(new TextBlock { Text = $"Solve #{row.Number} · {record.At.LocalDateTime:g}", Opacity = 0.7 });
        content.Children.Add(scramble);
        content.Children.Add(penalty);
        content.Children.Add(comment);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot!,
            Title = TimeFormat.Format(record.Time),
            Content = content,
            PrimaryButtonText = "Save",
            SecondaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            var updated = record with
            {
                Penalty = (Penalty)Math.Max(0, penalty.SelectedIndex),
                Comment = string.IsNullOrWhiteSpace(comment.Text) ? null : comment.Text.Trim(),
            };
            if (updated != record)
            {
                await ViewModel.UpdateSolveAsync(updated);
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            var settings = App.Services.GetRequiredService<SettingsService>();
            if (!settings.ConfirmDelete || await Dialogs.ConfirmAsync(XamlRoot!, "Delete solve", $"Delete solve #{row.Number} ({row.Time})?"))
            {
                await ViewModel.DeleteSolveAsync(record);
            }
        }
    }
}
