using SpeedcubingTrainer.Core.Persistence;

namespace SpeedcubingTrainer.Core.Tests.Persistence;

internal sealed class MemoryStorage : IAppStorage
{
    public Dictionary<string, string> Files { get; } = new();

    public int Writes { get; private set; }

    public Task<string?> ReadTextAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.TryGetValue(name, out var text) ? text : null);

    public Task WriteTextAsync(string name, string text, CancellationToken cancellationToken = default)
    {
        Writes++;
        Files[name] = text;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(Files.ContainsKey(name));

    public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        Files.Remove(name);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListAsync(string folder, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(Files.Keys.Where(k => k.StartsWith(folder, StringComparison.Ordinal)).OrderBy(k => k).ToList());
}
