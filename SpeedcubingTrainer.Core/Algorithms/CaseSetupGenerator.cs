using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Scrambling;

namespace SpeedcubingTrainer.Core.Algorithms;

/// <summary>
/// Builds setup scrambles for training: an optional whole-cube y rotation, a random U turn, the
/// inverse of the case's algorithm, and another random U turn. Applied to a solved cube this shows the
/// case from a random angle with random last-layer colours.
/// </summary>
public sealed class CaseSetupGenerator(IRandomSource random)
{
    public CaseSetup Create(AlgCase algCase, CaseSetupOptions options, int algorithmIndex = 0)
    {
        var algorithm = algCase.Algorithms[Math.Clamp(algorithmIndex, 0, algCase.Algorithms.Count - 1)];
        var setup = Algorithm.Empty;
        Move? y = null;
        Move? pre = null;
        Move? post = null;

        if (options.YRotation && random.Next(4) is var turns and > 0)
        {
            y = new Move(MoveTarget.Y, (Turn)turns);
            setup = setup.Append(y.Value);
        }
        if (options.PreAuf && random.Next(4) is var preTurns and > 0)
        {
            pre = new Move(MoveTarget.U, (Turn)preTurns);
            setup = setup.Append(pre.Value);
        }
        setup = setup.Concat(algorithm.Inverse());
        if (options.PostAuf && random.Next(4) is var postTurns and > 0)
        {
            post = new Move(MoveTarget.U, (Turn)postTurns);
            setup = setup.Append(post.Value);
        }
        setup = setup.Simplify();
        var state = CubeState.Solved.Apply(setup);
        return new CaseSetup(algCase, setup, state, y, pre, post);
    }
}
