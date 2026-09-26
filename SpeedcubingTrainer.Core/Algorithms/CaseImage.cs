using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Algorithms;

/// <summary>
/// The stickers needed to draw a case: the top face in net order and the top row of the F, R, B and L
/// faces (each read left to right as seen from the front, so the diagram can be drawn around the top face).
/// </summary>
public sealed record CaseImage(CubeState State, Face[] Top, Face[] Front, Face[] Right, Face[] Back, Face[] Left)
{
    public static CaseImage From(CubeState state)
    {
        var top = state.GetFace(Face.U).ToArray();
        return new CaseImage(
            state,
            top,
            state.GetFace(Face.F)[..3].ToArray(),
            state.GetFace(Face.R)[..3].ToArray(),
            state.GetFace(Face.B)[..3].ToArray(),
            state.GetFace(Face.L)[..3].ToArray());
    }
}

/// <summary>A scramble that produces a case on a solved cube.</summary>
public sealed record CaseSetup(AlgCase Case, Algorithm Setup, CubeState State, Move? YRotation, Move? PreAuf, Move? PostAuf)
{
    /// <summary>The moves that solve the set-up cube: undo the post-AUF, run the algorithm, undo the pre-AUF.</summary>
    public Algorithm Solution
    {
        get
        {
            var solution = Algorithm.Empty;
            if (PostAuf is { } post)
            {
                solution = solution.Append(post.Inverse);
            }
            solution = solution.Concat(Case.Primary);
            if (PreAuf is { } pre)
            {
                solution = solution.Append(pre.Inverse);
            }
            return solution;
        }
    }
}
