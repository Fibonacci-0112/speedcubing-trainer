using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Scrambling;

public enum ScrambleKind
{
    /// <summary>A uniformly random cube state, solved with the two-phase solver and inverted.</summary>
    RandomState,
    /// <summary>A fixed-length sequence of random face moves.</summary>
    RandomMoves,
}

/// <summary>A scramble sequence together with the state it produces from a solved cube.</summary>
public sealed record Scramble(Algorithm Algorithm, CubeState State, ScrambleKind Kind)
{
    public override string ToString() => Algorithm.ToString();
}

public interface IScrambler
{
    ScrambleKind Kind { get; }

    ValueTask<Scramble> NextAsync(CancellationToken cancellationToken = default);
}
