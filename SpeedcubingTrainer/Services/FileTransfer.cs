using Windows.Storage;
using Windows.Storage.Pickers;

namespace SpeedcubingTrainer.Services;

/// <summary>Saves and opens text files through the platform pickers, falling back to copy/paste dialogs.</summary>
public static class FileTransfer
{
    public static async Task SaveAsync(XamlRoot xamlRoot, string suggestedName, string content, string typeLabel, string extension)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = suggestedName, SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add(typeLabel, [extension]);
            PickerHost.Initialize(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return;
            }
            CachedFileManager.DeferUpdates(file);
            await FileIO.WriteTextAsync(file, content);
            await CachedFileManager.CompleteUpdatesAsync(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save picker unavailable: {ex.Message}");
            await Dialogs.ShowTextAsync(xamlRoot, $"Copy your {typeLabel}", content);
        }
    }

    public static async Task<string?> OpenTextAsync(XamlRoot xamlRoot, string extension)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeFilter.Add(extension);
            PickerHost.Initialize(picker);
            var file = await picker.PickSingleFileAsync();
            return file is null ? null : await FileIO.ReadTextAsync(file);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Open picker unavailable: {ex.Message}");
            return await Dialogs.PasteTextAsync(xamlRoot, "Import JSON");
        }
    }
}

internal static class PickerHost
{
    public static void Initialize(object picker)
    {
#if WINDOWS
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
#endif
    }
}
