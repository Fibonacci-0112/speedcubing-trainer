using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Solver;
using Xunit.Abstractions;

namespace SpeedcubingTrainer.Core.Tests.Solver;

public class SolverTablesTests(ITestOutputHelper output)
{
    [Fact]
    public void SerializeRoundTrips()
    {
        var tables = SharedTables.Instance;
        var blob = tables.Serialize();
        output.WriteLine($"Blob size {blob.Length / 1024} KB");
        var copy = SolverTables.TryDeserialize(blob);
        Assert.NotNull(copy);
        Assert.Equal(tables.TwistMove, copy.TwistMove);
        Assert.Equal(tables.CornerPermMove, copy.CornerPermMove);
        Assert.Equal(tables.TwistSlicePrune, copy.TwistSlicePrune);
        Assert.Equal(tables.EdgeSlicePrune, copy.EdgeSlicePrune);
    }

    [Fact]
    public void CorruptBlobsAreRejected()
    {
        var blob = SharedTables.Instance.Serialize();
        var corrupt = (byte[])blob.Clone();
        corrupt[blob.Length / 2] ^= 0xFF;
        Assert.Null(SolverTables.TryDeserialize(corrupt));

        var wrongVersion = (byte[])blob.Clone();
        wrongVersion[4] = 9;
        Assert.Null(SolverTables.TryDeserialize(wrongVersion));

        Assert.Null(SolverTables.TryDeserialize(blob.AsSpan(0, 100)));
        Assert.Null(SolverTables.TryDeserialize([]));
    }

    [Fact]
    public void PruneTablesHaveExpectedMaximumDepths()
    {
        var tables = SharedTables.Instance;
        Assert.InRange(tables.TwistSlicePrune.Max(), 8, 12);
        Assert.InRange(tables.FlipSlicePrune.Max(), 8, 12);
        Assert.InRange(tables.CornerSlicePrune.Max(), 10, 15);
        Assert.InRange(tables.EdgeSlicePrune.Max(), 8, 12);
        Assert.Equal(0, tables.TwistSlicePrune[0]);
        Assert.Equal(0, tables.EdgeSlicePrune[0]);
    }

    [Fact]
    public async Task ProviderBuildsOnceAndUsesCache()
    {
        var storage = new MemoryBlobStorage();
        var provider = new SolverTableProvider(storage, useThreadPool: true);
        Assert.False(provider.IsReady);
        var progress = new List<double>();
        var first = provider.GetAsync(new Progress<double>(progress.Add));
        var second = provider.GetAsync();
        var tables = await first;
        Assert.Same(tables, await second);
        Assert.True(provider.IsReady);
        Assert.NotNull(provider.LastBuildDuration);
        output.WriteLine($"Build took {provider.LastBuildDuration!.Value.TotalMilliseconds:F0} ms");
        Assert.Equal(1, storage.Writes);

        var cached = new SolverTableProvider(storage, useThreadPool: true);
        var fromCache = await cached.GetAsync();
        Assert.Null(cached.LastBuildDuration);
        Assert.Equal(tables.TwistMove, fromCache.TwistMove);
        Assert.Equal(1, storage.Writes);
    }

    private sealed class MemoryBlobStorage : IBlobStorage
    {
        private readonly Dictionary<string, byte[]> _blobs = new();

        public int Writes { get; private set; }

        public Task<byte[]?> ReadBytesAsync(string name, CancellationToken cancellationToken = default) =>
            Task.FromResult(_blobs.TryGetValue(name, out var blob) ? blob : null);

        public Task WriteBytesAsync(string name, byte[] data, CancellationToken cancellationToken = default)
        {
            Writes++;
            _blobs[name] = data;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string name, CancellationToken cancellationToken = default)
        {
            _blobs.Remove(name);
            return Task.CompletedTask;
        }
    }
}
