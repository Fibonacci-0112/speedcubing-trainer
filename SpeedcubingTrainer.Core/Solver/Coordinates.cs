using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Solver;

/// <summary>
/// Encodes parts of a <see cref="CubieCube"/> as small integers ("coordinates") and back.
/// Every coordinate is 0 for the solved cube.
/// </summary>
public static class Coordinates
{
    public const int TwistCount = 2187;       // 3^7
    public const int FlipCount = 2048;        // 2^11
    public const int SliceCount = 495;        // C(12,4)
    public const int CornerPermCount = 40320; // 8!
    public const int UdEdgePermCount = 40320; // 8!
    public const int SlicePermCount = 24;     // 4!

    private static readonly int[][] Binomial = BuildBinomial(13);
    private static readonly int[] Factorial = [1, 1, 2, 6, 24, 120, 720, 5040, 40320, 362880, 3628800, 39916800, 479001600];

    public static int GetTwist(CubieCube cube)
    {
        var twist = 0;
        for (var i = 0; i < 7; i++)
        {
            twist = twist * 3 + cube.Co[i];
        }
        return twist;
    }

    public static void SetTwist(CubieCube cube, int twist)
    {
        var sum = 0;
        for (var i = 6; i >= 0; i--)
        {
            cube.Co[i] = (byte)(twist % 3);
            sum += cube.Co[i];
            twist /= 3;
        }
        cube.Co[7] = (byte)((3 - sum % 3) % 3);
    }

    public static int GetFlip(CubieCube cube)
    {
        var flip = 0;
        for (var i = 0; i < 11; i++)
        {
            flip = flip * 2 + cube.Eo[i];
        }
        return flip;
    }

    public static void SetFlip(CubieCube cube, int flip)
    {
        var sum = 0;
        for (var i = 10; i >= 0; i--)
        {
            cube.Eo[i] = (byte)(flip & 1);
            sum += cube.Eo[i];
            flip >>= 1;
        }
        cube.Eo[11] = (byte)(sum & 1);
    }

    /// <summary>Which four slots hold the FR, FL, BL, BR edges, ignoring their order. 0 when they are all in the slice.</summary>
    public static int GetSlice(CubieCube cube)
    {
        var rank = 0;
        var found = 0;
        for (var q = 0; q < 12; q++)
        {
            if (cube.Ep[11 - q] >= (byte)Edge.FR)
            {
                rank += Binomial[q][found + 1];
                found++;
            }
        }
        return rank;
    }

    public static void SetSlice(CubieCube cube, int slice)
    {
        Span<bool> isSliceSlot = stackalloc bool[12];
        for (var k = 3; k >= 0; k--)
        {
            var q = k;
            while (Binomial[q + 1][k + 1] <= slice)
            {
                q++;
            }
            isSliceSlot[11 - q] = true;
            slice -= Binomial[q][k + 1];
        }
        byte nextSlice = (byte)Edge.FR;
        byte nextUd = 0;
        for (var slot = 0; slot < 12; slot++)
        {
            cube.Ep[slot] = isSliceSlot[slot] ? nextSlice++ : nextUd++;
        }
    }

    public static int GetCornerPerm(CubieCube cube) => Rank(cube.Cp, 0, 8);

    public static void SetCornerPerm(CubieCube cube, int rank) => Unrank(cube.Cp, 0, 8, rank);

    /// <summary>Permutation of the eight U/D edges among the U/D slots. Only meaningful when the slice edges are in the slice.</summary>
    public static int GetUdEdgePerm(CubieCube cube) => Rank(cube.Ep, 0, 8);

    public static void SetUdEdgePerm(CubieCube cube, int rank)
    {
        Unrank(cube.Ep, 0, 8, rank);
        for (var i = 8; i < 12; i++)
        {
            cube.Ep[i] = (byte)i;
        }
    }

    public static int GetSlicePerm(CubieCube cube) => Rank(cube.Ep, 8, 4);

    public static void SetSlicePerm(CubieCube cube, int rank)
    {
        Unrank(cube.Ep, 8, 4, rank);
        for (var i = 0; i < 8; i++)
        {
            cube.Ep[i] = (byte)i;
        }
    }

    /// <summary>Lehmer rank of the values perm[offset..offset+count), which must be a permutation of offset..offset+count-1.</summary>
    private static int Rank(byte[] perm, int offset, int count)
    {
        var rank = 0;
        for (var i = 0; i < count; i++)
        {
            var smallerAfter = 0;
            for (var j = i + 1; j < count; j++)
            {
                if (perm[offset + j] < perm[offset + i])
                {
                    smallerAfter++;
                }
            }
            rank += smallerAfter * Factorial[count - 1 - i];
        }
        return rank;
    }

    private static void Unrank(byte[] perm, int offset, int count, int rank)
    {
        Span<byte> remaining = stackalloc byte[count];
        for (var i = 0; i < count; i++)
        {
            remaining[i] = (byte)(offset + i);
        }
        var remainingCount = count;
        for (var i = 0; i < count; i++)
        {
            var f = Factorial[count - 1 - i];
            var d = rank / f;
            rank %= f;
            perm[offset + i] = remaining[d];
            for (var j = d; j < remainingCount - 1; j++)
            {
                remaining[j] = remaining[j + 1];
            }
            remainingCount--;
        }
    }

    private static int[][] BuildBinomial(int n)
    {
        var table = new int[n + 1][];
        for (var i = 0; i <= n; i++)
        {
            table[i] = new int[n + 1];
            table[i][0] = 1;
            for (var k = 1; k <= i; k++)
            {
                table[i][k] = table[i - 1][k - 1] + table[i - 1][k];
            }
        }
        return table;
    }
}
