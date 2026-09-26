using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Solver;
using SpeedcubingTrainer.Core.Tests.Solver;

namespace SpeedcubingTrainer.Core.Tests.Scrambling;

public class ScramblerTests
{
    [Fact]
    public void RandomMoveScrambleFollowsRules()
    {
        var scrambler = new RandomMoveScrambler(new SeededRandom(1));
        for (var trial = 0; trial < 200; trial++)
        {
            var scramble = scrambler.Next();
            var moves = scramble.Algorithm;
            Assert.Equal(25, moves.Count);
            for (var i = 1; i < moves.Count; i++)
            {
                Assert.True(moves[i].IsFaceMove);
                Assert.NotEqual(moves[i - 1].Face, moves[i].Face);
                if (i >= 2)
                {
                    Assert.False(moves[i].Axis == moves[i - 1].Axis && moves[i].Axis == moves[i - 2].Axis);
                }
            }
            Assert.Equal(CubeState.Solved.Apply(moves), scramble.State);
            Assert.Equal(ScrambleKind.RandomMoves, scramble.Kind);
        }
    }

    [Fact]
    public void RandomMoveScramblesAreReproducibleBySeed()
    {
        var a = new RandomMoveScrambler(new SeededRandom(42)).Next();
        var b = new RandomMoveScrambler(new SeededRandom(42)).Next();
        var c = new RandomMoveScrambler(new SeededRandom(43)).Next();
        Assert.Equal(a.Algorithm, b.Algorithm);
        Assert.NotEqual(a.Algorithm, c.Algorithm);
    }

    [Fact]
    public void RandomStatesAreValidAndCoverBothParities()
    {
        var rng = new SeededRandom(9);
        var oddSeen = false;
        var evenSeen = false;
        for (var i = 0; i < 1000; i++)
        {
            var cube = RandomCubeState.Next(rng);
            Assert.Equal(CubeValidity.Valid, cube.Validate());
            if (cube.CornerParity == 1)
            {
                oddSeen = true;
            }
            else
            {
                evenSeen = true;
            }
        }
        Assert.True(oddSeen && evenSeen);
    }

    [Fact]
    public async Task RandomStateScramblerProducesVerifiedScrambles()
    {
        var provider = new SolverTableProvider(useThreadPool: true);
        var scrambler = new RandomStateScrambler(new PrebuiltProvider().Provider, new SeededRandom(77), useThreadPool: false);
        for (var i = 0; i < 5; i++)
        {
            var scramble = await scrambler.NextAsync();
            Assert.Equal(ScrambleKind.RandomState, scramble.Kind);
            Assert.InRange(scramble.Algorithm.Count, 1, 24);
            Assert.Equal(scramble.State, CubeState.Solved.Apply(scramble.Algorithm));
            Assert.All(scramble.Algorithm, m => Assert.True(m.IsFaceMove));
        }
        _ = provider;
    }

    private sealed class PrebuiltProvider
    {
        public SolverTableProvider Provider { get; } = new(new SharedBlob(), useThreadPool: true);

        private sealed class SharedBlob : SpeedcubingTrainer.Core.Persistence.IBlobStorage
        {
            public Task<byte[]?> ReadBytesAsync(string name, CancellationToken cancellationToken = default) =>
                Task.FromResult<byte[]?>(SharedTables.Instance.Serialize());

            public Task WriteBytesAsync(string name, byte[] data, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task DeleteAsync(string name, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
