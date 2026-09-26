using SpeedcubingTrainer.Core.Persistence;
using Windows.Storage;

namespace SpeedcubingTrainer.Services;

/// <summary>
/// Owns the app's local data folder. On WebAssembly the folder is backed by IndexedDB and is only
/// usable after an awaited StorageFolder call, so everything that touches files goes through
/// <see cref="GetStorageAsync"/>.
/// </summary>
public sealed class AppDataService
{
    private readonly Lazy<Task<FileAppStorage>> _storage = new(InitializeAsync);

    public Task<FileAppStorage> GetStorageAsync() => _storage.Value;

    private static async Task<FileAppStorage> InitializeAsync()
    {
        var localFolder = ApplicationData.Current.LocalFolder;
        var dataFolder = await localFolder.CreateFolderAsync("data", CreationCollisionOption.OpenIfExists);
        return new FileAppStorage(dataFolder.Path, WasmStorageFlush.Request);
    }
}

/// <summary>
/// On WebAssembly the file system lives in memory and is copied to IndexedDB by the bootstrapper every
/// 10 seconds and on unload. A solve recorded just before closing the tab could be lost, so writes ask
/// for a flush shortly after they happen (debounced, because concurrent flushes are ignored).
/// </summary>
internal static partial class WasmStorageFlush
{
#if __WASM__
    private static readonly System.Threading.Timer Timer = new(_ => Flush(), null, System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);

    public static void Request() => Timer.Change(400, System.Threading.Timeout.Infinite);

    private static void Flush()
    {
        try
        {
            Synchronize(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"IndexedDB flush failed: {ex.Message}");
        }
    }

    [System.Runtime.InteropServices.JavaScript.JSImport("globalThis.Windows.Storage.StorageFolder.synchronizeFileSystem")]
    private static partial void Synchronize(bool populate);
#else
    public static void Request()
    {
    }
#endif
}
