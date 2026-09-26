using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Solver;

/// <summary>
/// Fills <see cref="SolverTables"/>. The work is exposed as an iterator that yields a progress value
/// after every chunk, so a single-threaded host (WebAssembly) can interleave it with UI work.
/// </summary>
internal static class TableBuilder
{
    private const byte Unvisited = 0xFF;
    private const int ChunkSize = 1 << 14;

    // Rough share of the total build time taken by each step, used only for progress reporting.
    private const double MoveTableShare = 0.15;

    public static IEnumerable<double> Build(SolverTables tables)
    {
        foreach (var p in BuildMoveTables(tables))
        {
            yield return p * MoveTableShare;
        }

        var pruneSteps = new (byte[] Table, int Rows, int RowStride, ushort[] RowMoves, ushort[] ColMoves, int MoveCount)[]
        {
            (tables.TwistSlicePrune, Coordinates.TwistCount, Coordinates.SliceCount, tables.TwistMove, tables.SliceMove, SolverTables.Phase1MoveCount),
            (tables.FlipSlicePrune, Coordinates.FlipCount, Coordinates.SliceCount, tables.FlipMove, tables.SliceMove, SolverTables.Phase1MoveCount),
            (tables.CornerSlicePrune, Coordinates.CornerPermCount, Coordinates.SlicePermCount, tables.CornerPermMove, tables.SlicePermMove, SolverTables.Phase2MoveCount),
            (tables.EdgeSlicePrune, Coordinates.UdEdgePermCount, Coordinates.SlicePermCount, tables.UdEdgePermMove, tables.SlicePermMove, SolverTables.Phase2MoveCount),
        };
        var remainingShare = (1 - MoveTableShare) / pruneSteps.Length;
        for (var s = 0; s < pruneSteps.Length; s++)
        {
            var step = pruneSteps[s];
            var offset = MoveTableShare + s * remainingShare;
            foreach (var p in BuildPruneTable(step.Table, step.Rows, step.RowStride, step.RowMoves, step.ColMoves, step.MoveCount))
            {
                yield return offset + p * remainingShare;
            }
        }
        yield return 1;
    }

    private static IEnumerable<double> BuildMoveTables(SolverTables tables)
    {
        var cube = new CubieCube();
        var scratch = new CubieCube();
        var moveCubes = CubieCube.MoveCubes;

        for (var twist = 0; twist < Coordinates.TwistCount; twist++)
        {
            Coordinates.SetTwist(cube, twist);
            for (var face = 0; face < 6; face++)
            {
                scratch.CopyFrom(cube);
                for (var turn = 0; turn < 3; turn++)
                {
                    scratch.MultiplyCorners(moveCubes[face]);
                    tables.TwistMove[twist * SolverTables.Phase1MoveCount + face * 3 + turn] = (ushort)Coordinates.GetTwist(scratch);
                }
            }
        }
        yield return 0.1;

        cube = new CubieCube();
        for (var flip = 0; flip < Coordinates.FlipCount; flip++)
        {
            Coordinates.SetFlip(cube, flip);
            for (var face = 0; face < 6; face++)
            {
                scratch.CopyFrom(cube);
                for (var turn = 0; turn < 3; turn++)
                {
                    scratch.MultiplyEdges(moveCubes[face]);
                    tables.FlipMove[flip * SolverTables.Phase1MoveCount + face * 3 + turn] = (ushort)Coordinates.GetFlip(scratch);
                }
            }
        }
        yield return 0.2;

        cube = new CubieCube();
        for (var slice = 0; slice < Coordinates.SliceCount; slice++)
        {
            Coordinates.SetSlice(cube, slice);
            for (var face = 0; face < 6; face++)
            {
                scratch.CopyFrom(cube);
                for (var turn = 0; turn < 3; turn++)
                {
                    scratch.MultiplyEdges(moveCubes[face]);
                    tables.SliceMove[slice * SolverTables.Phase1MoveCount + face * 3 + turn] = (ushort)Coordinates.GetSlice(scratch);
                }
            }
        }
        yield return 0.25;

        cube = new CubieCube();
        for (var perm = 0; perm < Coordinates.CornerPermCount; perm++)
        {
            Coordinates.SetCornerPerm(cube, perm);
            for (var m = 0; m < SolverTables.Phase2MoveCount; m++)
            {
                var face = SolverTables.Phase2Moves[m] / 3;
                var turns = SolverTables.Phase2Moves[m] % 3 + 1;
                scratch.CopyFrom(cube);
                for (var t = 0; t < turns; t++)
                {
                    scratch.MultiplyCorners(moveCubes[face]);
                }
                tables.CornerPermMove[perm * SolverTables.Phase2MoveCount + m] = (ushort)Coordinates.GetCornerPerm(scratch);
            }
            if ((perm & (ChunkSize - 1)) == ChunkSize - 1)
            {
                yield return 0.25 + 0.35 * perm / Coordinates.CornerPermCount;
            }
        }
        yield return 0.6;

        cube = new CubieCube();
        for (var perm = 0; perm < Coordinates.UdEdgePermCount; perm++)
        {
            Coordinates.SetUdEdgePerm(cube, perm);
            for (var m = 0; m < SolverTables.Phase2MoveCount; m++)
            {
                var face = SolverTables.Phase2Moves[m] / 3;
                var turns = SolverTables.Phase2Moves[m] % 3 + 1;
                scratch.CopyFrom(cube);
                for (var t = 0; t < turns; t++)
                {
                    scratch.MultiplyEdges(moveCubes[face]);
                }
                tables.UdEdgePermMove[perm * SolverTables.Phase2MoveCount + m] = (ushort)Coordinates.GetUdEdgePerm(scratch);
            }
            if ((perm & (ChunkSize - 1)) == ChunkSize - 1)
            {
                yield return 0.6 + 0.35 * perm / Coordinates.UdEdgePermCount;
            }
        }
        yield return 0.95;

        cube = new CubieCube();
        for (var perm = 0; perm < Coordinates.SlicePermCount; perm++)
        {
            Coordinates.SetSlicePerm(cube, perm);
            for (var m = 0; m < SolverTables.Phase2MoveCount; m++)
            {
                var face = SolverTables.Phase2Moves[m] / 3;
                var turns = SolverTables.Phase2Moves[m] % 3 + 1;
                scratch.CopyFrom(cube);
                for (var t = 0; t < turns; t++)
                {
                    scratch.MultiplyEdges(moveCubes[face]);
                }
                tables.SlicePermMove[perm * SolverTables.Phase2MoveCount + m] = (ushort)Coordinates.GetSlicePerm(scratch);
            }
        }
        yield return 1;
    }

    /// <summary>Breadth-first distance from the solved pair (row 0, column 0) over the product of two coordinates.</summary>
    private static IEnumerable<double> BuildPruneTable(byte[] table, int rows, int cols, ushort[] rowMoves, ushort[] colMoves, int moveCount)
    {
        Array.Fill(table, Unvisited);
        table[0] = 0;
        var visited = 1;
        var total = table.Length;
        byte depth = 0;
        while (visited < total)
        {
            var found = 0;
            for (var index = 0; index < total; index++)
            {
                if (table[index] != depth)
                {
                    if ((index & (ChunkSize - 1)) == ChunkSize - 1)
                    {
                        yield return Math.Min(0.99, (double)visited / total);
                    }
                    continue;
                }
                var row = index / cols;
                var col = index - row * cols;
                for (var m = 0; m < moveCount; m++)
                {
                    var next = rowMoves[row * moveCount + m] * cols + colMoves[col * moveCount + m];
                    if (table[next] == Unvisited)
                    {
                        table[next] = (byte)(depth + 1);
                        found++;
                    }
                }
                if ((index & (ChunkSize - 1)) == ChunkSize - 1)
                {
                    yield return Math.Min(0.99, (double)visited / total);
                }
            }
            if (found == 0)
            {
                break;
            }
            visited += found;
            depth++;
        }
        for (var index = 0; index < total; index++)
        {
            if (table[index] == Unvisited)
            {
                throw new InvalidOperationException("Pruning table has unreachable entries; the move tables are inconsistent.");
            }
        }
        yield return 1;
    }
}
