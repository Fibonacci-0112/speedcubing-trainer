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

/// <summary>
/// One arrow on a last-layer diagram: the piece at top-face sticker <see cref="From"/> belongs at
/// <see cref="To"/>. A two-way arrow marks two pieces that swap places.
/// </summary>
public readonly record struct PieceArrow(int From, int To, bool TwoWay);

/// <summary>Works out the PLL-style arrows that show where each last-layer piece has to go.</summary>
public static class LastLayerArrows
{
    // Each last-layer piece by the top-face sticker it sits under, with its side stickers taken from
    // CaseImage (face, index in that face's top row).
    private static readonly (int Top, (Face Side, int Index)[] Sides)[] Pieces =
    [
        (0, [(Face.L, 0), (Face.B, 2)]),
        (1, [(Face.B, 1)]),
        (2, [(Face.B, 0), (Face.R, 2)]),
        (3, [(Face.L, 1)]),
        (5, [(Face.R, 1)]),
        (6, [(Face.F, 0), (Face.L, 2)]),
        (7, [(Face.F, 1)]),
        (8, [(Face.R, 0), (Face.F, 2)]),
    ];

    // Where a U turn carries each top-face position (clockwise seen from above).
    private static readonly int[] UTurn = [2, 5, 8, 1, 4, 7, 0, 3, 6];

    /// <summary>
    /// The arrows for a state whose last layer differs from solved only by moving pieces around. The
    /// final U adjustment that leaves the fewest pieces moving is assumed, as printed PLL sheets do.
    /// </summary>
    public static IReadOnlyList<PieceArrow> From(CubeState state)
    {
        var image = CaseImage.From(state);
        var solved = CaseImage.From(CubeState.Solved);

        // Where each piece belongs, keyed by its colours.
        var home = new Dictionary<int, int>();
        foreach (var piece in Pieces)
        {
            home[Key(solved, piece)] = piece.Top;
        }
        var current = new int[9];
        foreach (var piece in Pieces)
        {
            if (!home.TryGetValue(Key(image, piece), out current[piece.Top]))
            {
                return [];   // not a last-layer permutation (e.g. F2L is broken)
            }
        }

        // Ties (the G perms) go to the adjustment with the shortest cycles, so a G perm shows two
        // three-cycles rather than a swap plus a four-cycle.
        int[]? best = null;
        var bestScore = (Moved: int.MaxValue, LongestCycle: int.MaxValue);
        for (var auf = 0; auf < 4; auf++)
        {
            var target = new int[9];
            foreach (var piece in Pieces)
            {
                var to = current[piece.Top];
                for (var k = 0; k < auf; k++)
                {
                    to = UTurn[to];
                }
                target[piece.Top] = to;
            }
            var cycles = Cycles(target);
            var score = (Moved: cycles.Sum(c => c.Count), LongestCycle: cycles.Select(c => c.Count).DefaultIfEmpty(0).Max());
            if (score.Moved < bestScore.Moved || (score.Moved == bestScore.Moved && score.LongestCycle < bestScore.LongestCycle))
            {
                best = target;
                bestScore = score;
            }
        }

        var arrows = new List<PieceArrow>();
        foreach (var cycle in Cycles(best!))
        {
            if (cycle.Count == 2)
            {
                arrows.Add(new PieceArrow(cycle[0], cycle[1], TwoWay: true));
            }
            else
            {
                arrows.AddRange(cycle.Select(p => new PieceArrow(p, best![p], TwoWay: false)));
            }
        }
        return arrows;
    }

    /// <summary>The cycles of moving pieces in a position-to-target map (fixed pieces left out).</summary>
    private static List<List<int>> Cycles(int[] target)
    {
        var cycles = new List<List<int>>();
        var seen = new bool[9];
        foreach (var piece in Pieces)
        {
            if (seen[piece.Top] || target[piece.Top] == piece.Top)
            {
                continue;
            }
            var cycle = new List<int>();
            for (var p = piece.Top; !seen[p]; p = target[p])
            {
                seen[p] = true;
                cycle.Add(p);
            }
            cycles.Add(cycle);
        }
        return cycles;
    }

    private static int Key(CaseImage image, (int Top, (Face Side, int Index)[] Sides) piece)
    {
        var key = 1 << (int)image.Top[piece.Top];
        foreach (var (side, index) in piece.Sides)
        {
            var row = side switch
            {
                Face.F => image.Front,
                Face.R => image.Right,
                Face.B => image.Back,
                _ => image.Left,
            };
            key |= 1 << (int)row[index];
        }
        return key;
    }
}
