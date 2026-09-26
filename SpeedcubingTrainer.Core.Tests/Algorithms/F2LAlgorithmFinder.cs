using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Tests.Algorithms;

/// <summary>
/// Finds a short algorithm for an F2L case given its signature (see AlgorithmDataTests.F2LSignature),
/// by depth-first search over R, U, F (then L, D, B) moves. Used to make data failures actionable.
/// </summary>
internal static class F2LAlgorithmFinder
{
    public static string Suggest(string signature)
    {
        var parts = signature.TrimStart('c').Split(['o', 'e'], StringSplitOptions.RemoveEmptyEntries);
        var cornerSlot = int.Parse(parts[0]);
        var cornerOri = int.Parse(parts[1]);
        var edgeSlot = int.Parse(parts[2]);
        var edgeOri = int.Parse(parts[3]);
        var start = BuildState(cornerSlot, cornerOri, edgeSlot, edgeOri);
        if (start is null)
        {
            return "(could not build a valid state)";
        }
        Face[] ruf = [Face.R, Face.U, Face.F];
        Face[] all = [Face.R, Face.U, Face.F, Face.L, Face.D, Face.B];
        for (var depth = 1; depth <= 8; depth++)
        {
            var found = Search(start, ruf, depth);
            if (found is not null)
            {
                return found;
            }
        }
        // Cases with both pieces in the slot: take the pair out first, then solve the resulting case.
        var candidates = new List<string>();
        foreach (var prefix in new[] { "R U R'", "R U' R'", "R U2 R'", "F' U F", "F' U' F", "F' U2 F" })
        {
            var alg = Algorithm.Parse(prefix);
            var after = start.Clone();
            after.Apply(alg);
            for (var depth = 1; depth <= 8; depth++)
            {
                var found = Search(after, ruf, depth);
                if (found is not null)
                {
                    candidates.Add($"{prefix} {found}");
                    break;
                }
            }
        }
        if (candidates.Count > 0)
        {
            return candidates.OrderBy(c => c.Split(' ').Length).ThenBy(c => c.Count(ch => ch == '2')).First();
        }
        for (var depth = 1; depth <= 7; depth++)
        {
            var found = Search(start, all, depth);
            if (found is not null)
            {
                return found;
            }
        }
        return "(none within 8 moves)";
    }

    private static CubieCube? BuildState(int cornerSlot, int cornerOri, int edgeSlot, int edgeOri)
    {
        var cube = CubieCube.Solved;
        (cube.Cp[cornerSlot], cube.Cp[(int)Corner.DFR]) = (cube.Cp[(int)Corner.DFR], cube.Cp[cornerSlot]);
        cube.Co[cornerSlot] = (byte)cornerOri;
        (cube.Ep[edgeSlot], cube.Ep[(int)Edge.FR]) = (cube.Ep[(int)Edge.FR], cube.Ep[edgeSlot]);
        cube.Eo[edgeSlot] = (byte)edgeOri;
        // Repair the constraints using last-layer pieces that are not part of the pair.
        var lastLayerCorner = Enumerable.Range(0, 4).First(i => i != cornerSlot);
        cube.Co[lastLayerCorner] = (byte)((3 - (cube.Co.Sum(c => c) - cube.Co[lastLayerCorner]) % 3) % 3);
        var lastLayerEdge = Enumerable.Range(0, 4).First(i => i != edgeSlot);
        cube.Eo[lastLayerEdge] = (byte)((cube.Eo.Sum(e => e) - cube.Eo[lastLayerEdge]) & 1);
        if (cube.CornerParity != cube.EdgeParity)
        {
            var free = Enumerable.Range(0, 4).Where(i => i != edgeSlot).Take(2).ToArray();
            (cube.Ep[free[0]], cube.Ep[free[1]]) = (cube.Ep[free[1]], cube.Ep[free[0]]);
        }
        return cube.Validate() == CubeValidity.Valid ? cube : null;
    }

    private static string? Search(CubieCube start, Face[] faces, int depth)
    {
        var moves = new Move[depth];
        var cube = start.Clone();
        return Dfs(cube, faces, moves, 0, depth, -1) ? new Algorithm(moves).ToString() : null;
    }

    private static bool Dfs(CubieCube cube, Face[] faces, Move[] moves, int index, int depth, int lastFace)
    {
        if (index == depth)
        {
            return IsF2LDone(cube);
        }
        foreach (var face in faces)
        {
            if ((int)face == lastFace)
            {
                continue;
            }
            for (var turn = 1; turn <= 3; turn++)
            {
                var move = Move.Face_(face, (Turn)turn);
                var next = cube.Clone();
                next.Apply(move);
                moves[index] = move;
                if (Dfs(next, faces, moves, index + 1, depth, (int)face))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsF2LDone(CubieCube cube)
    {
        for (var i = 4; i < 8; i++)
        {
            if (cube.Cp[i] != i || cube.Co[i] != 0)
            {
                return false;
            }
        }
        for (var i = 4; i < 12; i++)
        {
            if (cube.Ep[i] != i || cube.Eo[i] != 0)
            {
                return false;
            }
        }
        return true;
    }
}
