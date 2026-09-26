namespace SpeedcubingTrainer.Core.Persistence;

/// <summary>Text file storage keyed by a relative path such as <c>sessions/index.json</c>.</summary>
public interface IAppStorage
{
    Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken = default);

    Task WriteTextAsync(string name, string text, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Lists the names stored under a folder prefix, e.g. <c>sessions/</c>.</summary>
    Task<IReadOnlyList<string>> ListAsync(string folder, CancellationToken cancellationToken = default);
}

/// <summary>Binary blob storage keyed by a relative path.</summary>
public interface IBlobStorage
{
    Task<byte[]?> ReadBytesAsync(string name, CancellationToken cancellationToken = default);

    Task WriteBytesAsync(string name, byte[] data, CancellationToken cancellationToken = default);

    Task DeleteAsync(string name, CancellationToken cancellationToken = default);
}
