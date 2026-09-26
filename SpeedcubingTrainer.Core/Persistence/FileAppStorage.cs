namespace SpeedcubingTrainer.Core.Persistence;

/// <summary>
/// File-based storage rooted at a directory the host chooses (the app's local data folder).
/// Writes go to a temporary file that is then moved over the target, and reads fall back to the
/// temporary file when the target is missing or empty, so an interrupted write never loses the
/// previous good copy.
/// </summary>
public sealed class FileAppStorage(string rootDirectory) : IAppStorage, IBlobStorage
{
    private const string TempSuffix = ".tmp";

    public string RootDirectory { get; } = Path.GetFullPath(rootDirectory);

    public async Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken = default)
    {
        var bytes = await ReadBytesAsync(name, cancellationToken).ConfigureAwait(false);
        return bytes is null ? null : System.Text.Encoding.UTF8.GetString(bytes);
    }

    public Task WriteTextAsync(string name, string text, CancellationToken cancellationToken = default) =>
        WriteBytesAsync(name, System.Text.Encoding.UTF8.GetBytes(text), cancellationToken);

    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = Resolve(name);
        return Task.FromResult(File.Exists(path) || File.Exists(path + TempSuffix));
    }

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = Resolve(name);
        File.Delete(path);
        File.Delete(path + TempSuffix);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string folder, CancellationToken cancellationToken = default)
    {
        var directory = Resolve(folder.TrimEnd('/'));
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }
        var prefix = folder.TrimEnd('/') + "/";
        var names = Directory.EnumerateFiles(directory)
            .Where(f => !f.EndsWith(TempSuffix, StringComparison.Ordinal))
            .Select(f => prefix + Path.GetFileName(f))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(names);
    }

    public async Task<byte[]?> ReadBytesAsync(string name, CancellationToken cancellationToken = default)
    {
        var path = Resolve(name);
        var bytes = await TryReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (bytes is { Length: > 0 })
        {
            return bytes;
        }
        var fallback = await TryReadAsync(path + TempSuffix, cancellationToken).ConfigureAwait(false);
        return fallback is { Length: > 0 } ? fallback : bytes;
    }

    public async Task WriteBytesAsync(string name, byte[] data, CancellationToken cancellationToken = default)
    {
        var path = Resolve(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + TempSuffix;
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
        {
            await stream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        File.Move(temp, path, overwrite: true);
    }

    private static async Task<byte[]?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private string Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(name))
        {
            throw new ArgumentException($"Invalid storage name '{name}'.", nameof(name));
        }
        return Path.Combine(RootDirectory, name.Replace('/', Path.DirectorySeparatorChar));
    }
}
