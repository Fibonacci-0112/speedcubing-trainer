using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Solver;

namespace SpeedcubingTrainer.Core.Scrambling;

/// <summary>
/// WCA-style random-state scrambles: draw a uniformly random cube, solve it with the two-phase solver
/// and return the inverse of the solution.
/// </summary>
public sealed class RandomStateScrambler(SolverTableProvider tables, IRandomSource random, SolverOptions? options = null, bool? useThreadPool = null) : IScrambler
{
    private readonly bool _useThreadPool = useThreadPool ?? !OperatingSystem.IsBrowser();
    private readonly SolverOptions _options = options ?? SolverOptions.Default;

    public ScrambleKind Kind => ScrambleKind.RandomState;

    public async ValueTask<Scramble> NextAsync(CancellationToken cancellationToken = default)
    {
        var solverTables = await tables.GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var solver = new TwoPhaseSolver(solverTables);
        return _useThreadPool
            ? await Task.Run(() => Generate(solver, cancellationToken), cancellationToken).ConfigureAwait(false)
            : Generate(solver, cancellationToken);
    }

    private Scramble Generate(TwoPhaseSolver solver, CancellationToken cancellationToken)
    {
        SolveResult? last = null;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cube = RandomCubeState.Next(random);
            var result = solver.Solve(cube, _options, cancellationToken);
            last = result;
            if (result.Solution is { } solution)
            {
                var scramble = solution.Inverse();
                var state = CubeState.FromCubieCube(cube);
                if (!CubeState.Solved.Apply(scramble).Equals(state))
                {
                    throw new InvalidOperationException("Solver produced a solution that does not reproduce the state.");
                }
                return new Scramble(scramble, state, ScrambleKind.RandomState);
            }
        }
        throw new TimeoutException($"Could not solve a random state within the budget (last status: {last?.Status}).");
    }
}
