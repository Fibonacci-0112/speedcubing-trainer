using SpeedcubingTrainer.Core.Solver;

namespace SpeedcubingTrainer.Core.Tests.Solver;

/// <summary>Builds the solver tables once for the whole test run.</summary>
internal static class SharedTables
{
    private static readonly Lazy<SolverTables> Lazy = new(() => SolverTableProvider.BuildAsync().GetAwaiter().GetResult());

    public static SolverTables Instance => Lazy.Value;
}
