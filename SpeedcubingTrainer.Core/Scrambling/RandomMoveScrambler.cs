using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Scrambling;

/// <summary>
/// Produces scrambles of random face moves where consecutive moves never turn the same face and
/// no three consecutive moves share an axis.
/// </summary>
public sealed class RandomMoveScrambler(IRandomSource random, int length = RandomMoveScrambler.DefaultLength) : IScrambler
{
    public const int DefaultLength = 25;

    public ScrambleKind Kind => ScrambleKind.RandomMoves;

    public int Length { get; } = length > 0 ? length : throw new ArgumentOutOfRangeException(nameof(length));

    public ValueTask<Scramble> NextAsync(CancellationToken cancellationToken = default) => new(Next());

    public Scramble Next()
    {
        var moves = new Move[Length];
        var previous = -1;
        var beforePrevious = -1;
        for (var i = 0; i < Length; i++)
        {
            int face;
            do
            {
                face = random.Next(6);
            }
            while (face == previous || (beforePrevious >= 0 && face % 3 == previous % 3 && face % 3 == beforePrevious % 3));

            moves[i] = new Move((MoveTarget)face, (Turn)(random.Next(3) + 1));
            beforePrevious = previous;
            previous = face;
        }
        var algorithm = new Algorithm(moves);
        return new Scramble(algorithm, CubeState.Solved.Apply(algorithm), ScrambleKind.RandomMoves);
    }
}
