namespace SpeedcubingTrainer.Core.Scrambling;

/// <summary>Source of random integers, abstracted so scrambles are reproducible in tests.</summary>
public interface IRandomSource
{
    /// <summary>Returns a value in [0, maxExclusive).</summary>
    int Next(int maxExclusive);
}

/// <summary>Deterministic random source backed by <see cref="Random"/>.</summary>
public sealed class SeededRandom(int seed) : IRandomSource
{
    private readonly Random _random = new(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);
}

/// <summary>Non-deterministic random source using the shared thread-safe generator.</summary>
public sealed class SystemRandom : IRandomSource
{
    public static readonly SystemRandom Instance = new();

    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
