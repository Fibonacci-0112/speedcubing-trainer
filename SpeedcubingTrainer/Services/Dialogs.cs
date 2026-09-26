namespace SpeedcubingTrainer.Services;

public static class Dialogs
{
    public static async Task<string?> PromptAsync(XamlRoot xamlRoot, string title, string header, string initial = "")
    {
        var box = new TextBox { Text = initial, Header = header, SelectionStart = initial.Length };
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    public static async Task<bool> ConfirmAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = "Yes",
            CloseButtonText = "No",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Returns false for the first option, true for the second, null when cancelled.</summary>
    public static async Task<bool?> ChooseAsync(XamlRoot xamlRoot, string title, string message, string first, string second)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = first,
            SecondaryButtonText = second,
            CloseButtonText = "Cancel",
        };
        return await dialog.ShowAsync() switch
        {
            ContentDialogResult.Primary => false,
            ContentDialogResult.Secondary => true,
            _ => null,
        };
    }

    public static Task MessageAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
            CloseButtonText = "OK",
        };
        return dialog.ShowAsync().AsTask();
    }

    /// <summary>Shows text the user can copy, as a fallback when file pickers are unavailable.</summary>
    public static Task ShowTextAsync(XamlRoot xamlRoot, string title, string text)
    {
        var box = new TextBox { Text = text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 320 };
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new ScrollViewer { Content = box },
            CloseButtonText = "Close",
        };
        return dialog.ShowAsync().AsTask();
    }

    public static async Task<string?> PasteTextAsync(XamlRoot xamlRoot, string title)
    {
        var box = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 200, PlaceholderText = "Paste the exported JSON here" };
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = new ScrollViewer { Content = box },
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }
}
