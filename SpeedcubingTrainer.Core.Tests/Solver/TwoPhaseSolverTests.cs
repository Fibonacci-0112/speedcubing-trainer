using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;
using SpeedcubingTrainer.Core.Solver;
using Xunit.Abstractions;

namespace SpeedcubingTrainer.Core.Tests.Solver;

public class TwoPhaseSolverTests(ITestOutputHelper output)
{
    [Fact]
    public void SolvedCubeNeedsNoMoves()
    {
        var result = new TwoPhaseSolver(SharedTables.Instance).Solve(CubieCube.Solved);
        Assert.Equal(SolveStatus.Found, result.Status);
        Assert.Empty(result.Solution!);
    }

    [Theory]
    [InlineData("R", 1, 1)]
    [InlineData("U2", 1, 1)]
    [InlineData("R U", 2, 2)]
    [InlineData("R U F", 3, 3)]
    [InlineData("F R U R' U' F'", 6, 12)]
    public void ShortScramblesGetShortSolutions(string scramble, int optimal, int acceptable)
    {
        var cube = CubieCube.Solved;
        cube.Apply(Algorithm.Parse(scramble));
        var result = new TwoPhaseSolver(SharedTables.Instance).Solve(cube);
        Assert.Equal(SolveStatus.Found, result.Status);
        output.WriteLine($"{scramble} -> {result.Solution} ({result.Solution!.Count} moves)");
        Assert.InRange(result.Solution.Count, optimal, acceptable);
        cube.Apply(result.Solution);
        Assert.True(cube.IsSolved);
    }

    [Fact]
    public void SuperflipIsSolvedWithinLimit()
    {
        var superflip = CubieCube.Solved;
        superflip.Apply(Algorithm.Parse("U R2 F B R B2 R U2 L B2 R U' D' R2 F R' L B2 U2 F2"));
        for (var i = 0; i < 12; i++)
        {
            Assert.Equal(1, superflip.Eo[i]);
        }
        var result = new TwoPhaseSolver(SharedTables.Instance).Solve(superflip);
        Assert.Equal(SolveStatus.Found, result.Status);
        Assert.InRange(result.Solution!.Count, 20, 24);
        superflip.Apply(result.Solution);
        Assert.True(superflip.IsSolved);
    }

    [Fact]
    public void RandomStatesAreSolvedWithinLimit()
    {
        var solver = new TwoPhaseSolver(SharedTables.Instance);
        var rng = new SeededRandom(2026);
        var totalMs = 0.0;
        var maxLength = 0;
        for (var i = 0; i < 20; i++)
        {
            var cube = RandomCubeState.Next(rng);
            var result = solver.Solve(cube);
            Assert.Equal(SolveStatus.Found, result.Status);
            Assert.InRange(result.Solution!.Count, 0, 24);
            var check = cube.Clone();
            check.Apply(result.Solution);
            Assert.True(check.IsSolved);
            var facelets = CubeState.FromCubieCube(cube).Apply(result.Solution);
            Assert.True(facelets.IsSolved);
            totalMs += result.Elapsed.TotalMilliseconds;
            maxLength = Math.Max(maxLength, result.Solution.Count);
        }
        output.WriteLine($"20 solves in {totalMs:F1} ms, longest {maxLength} moves");
    }

    [Fact]
    public void SolutionsRespectMoveOrderingRules()
    {
        var solver = new TwoPhaseSolver(SharedTables.Instance);
        var rng = new SeededRandom(5);
        for (var i = 0; i < 10; i++)
        {
            var result = solver.Solve(RandomCubeState.Next(rng));
            var moves = result.Solution!;
            for (var k = 1; k < moves.Count; k++)
            {
                Assert.NotEqual(moves[k - 1].Face, moves[k].Face);
            }
        }
    }

    [Fact]
    public void TinyBudgetTimesOutCleanly()
    {
        var solver = new TwoPhaseSolver(SharedTables.Instance);
        var cube = RandomCubeState.Next(new SeededRandom(11));
        var result = solver.Solve(cube, new SolverOptions(MaxLength: 18, TargetLength: 18, TimeBudget: TimeSpan.Zero));
        Assert.Equal(SolveStatus.TimedOut, result.Status);
        Assert.Null(result.Solution);
    }

    [Fact]
    public void InvalidCubeIsReported()
    {
        var cube = CubieCube.Solved;
        cube.Co[0] = 1;
        Assert.Equal(SolveStatus.Invalid, new TwoPhaseSolver(SharedTables.Instance).Solve(cube).Status);
    }
}
