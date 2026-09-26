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
        return new FileAppStorage(dataFolder.Path);
    }
}
