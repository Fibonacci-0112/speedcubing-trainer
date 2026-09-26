namespace SpeedcubingTrainer.Core.Cube;

public enum Corner : byte { URF, UFL, ULB, UBR, DFR, DLF, DBL, DRB }

public enum Edge : byte { UR, UF, UL, UB, DR, DF, DL, DB, FR, FL, BL, BR }

public enum CubeValidity
{
    Valid,
    DuplicatePiece,
    CornerTwist,
    EdgeFlip,
    PermutationParity,
}

/// <summary>Piece definitions shared by the facelet and cubie models.</summary>
public static class CubeDefinitions
{
    /// <summary>Facelet indices of each corner: the U/D sticker first, then clockwise around the corner.</summary>
    public static readonly int[][] CornerFacelets =
    [
        [8, 9, 20],   // URF: U9 R1 F3
        [6, 18, 38],  // UFL: U7 F1 L3
        [0, 36, 47],  // ULB: U1 L1 B3
        [2, 45, 11],  // UBR: U3 B1 R3
        [29, 26, 15], // DFR: D3 F9 R7
        [27, 44, 24], // DLF: D1 L9 F7
        [33, 53, 42], // DBL: D7 B9 L7
        [35, 17, 51], // DRB: D9 R9 B7
    ];

    /// <summary>Facelet indices of each edge: the U/D sticker first for U/D edges, the F/B sticker first for slice edges.</summary>
    public static readonly int[][] EdgeFacelets =
    [
        [5, 10],  // UR: U6 R2
        [7, 19],  // UF: U8 F2
        [3, 37],  // UL: U4 L2
        [1, 46],  // UB: U2 B2
        [32, 16], // DR: D6 R8
        [28, 25], // DF: D2 F8
        [30, 43], // DL: D4 L8
        [34, 52], // DB: D8 B8
        [23, 12], // FR: F6 R4
        [21, 41], // FL: F4 L6
        [50, 39], // BL: B6 L4
        [48, 14], // BR: B4 R6
    ];

    public static readonly Face[][] CornerColors =
    [
        [Face.U, Face.R, Face.F],
        [Face.U, Face.F, Face.L],
        [Face.U, Face.L, Face.B],
        [Face.U, Face.B, Face.R],
        [Face.D, Face.F, Face.R],
        [Face.D, Face.L, Face.F],
        [Face.D, Face.B, Face.L],
        [Face.D, Face.R, Face.B],
    ];

    public static readonly Face[][] EdgeColors =
    [
        [Face.U, Face.R],
        [Face.U, Face.F],
        [Face.U, Face.L],
        [Face.U, Face.B],
        [Face.D, Face.R],
        [Face.D, Face.F],
        [Face.D, Face.L],
        [Face.D, Face.B],
        [Face.F, Face.R],
        [Face.F, Face.L],
        [Face.B, Face.L],
        [Face.B, Face.R],
    ];
}

/// <summary>
/// Mutable piece-level model of the cube used by the solver: which corner/edge sits in each slot and how it is
/// twisted or flipped. Centres are fixed, so only face moves apply. <c>Cp[i]</c> is the corner that occupies
/// slot <c>i</c>; <c>Co[i]</c> is its clockwise twist (0..2); edges likewise with <c>Eo</c> in {0, 1}.
/// </summary>
public sealed class CubieCube : IEquatable<CubieCube>
{
    public readonly byte[] Cp = new byte[8];
    public readonly byte[] Co = new byte[8];
    public readonly byte[] Ep = new byte[12];
    public readonly byte[] Eo = new byte[12];

    /// <summary>The six basic clockwise face moves as cubie permutations, indexed by <see cref="Face"/>.</summary>
    public static readonly CubieCube[] MoveCubes = BuildMoveCubes();

    public CubieCube()
    {
        for (var i = 0; i < 8; i++)
        {
            Cp[i] = (byte)i;
        }
        for (var i = 0; i < 12; i++)
        {
            Ep[i] = (byte)i;
        }
    }

    public CubieCube(ReadOnlySpan<byte> cp, ReadOnlySpan<byte> co, ReadOnlySpan<byte> ep, ReadOnlySpan<byte> eo)
    {
        cp.CopyTo(Cp);
        co.CopyTo(Co);
        ep.CopyTo(Ep);
        eo.CopyTo(Eo);
    }

    public static CubieCube Solved => new();

    public CubieCube Clone() => new(Cp, Co, Ep, Eo);

    public void CopyFrom(CubieCube other)
    {
        other.Cp.CopyTo(Cp, 0);
        other.Co.CopyTo(Co, 0);
        other.Ep.CopyTo(Ep, 0);
        other.Eo.CopyTo(Eo, 0);
    }

    public bool IsSolved
    {
        get
        {
            for (var i = 0; i < 8; i++)
            {
                if (Cp[i] != i || Co[i] != 0)
                {
                    return false;
                }
            }
            for (var i = 0; i < 12; i++)
            {
                if (Ep[i] != i || Eo[i] != 0)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>Applies <paramref name="other"/> after this cube's current state: this = this * other.</summary>
    public void Multiply(CubieCube other)
    {
        MultiplyCorners(other);
        MultiplyEdges(other);
    }

    public void MultiplyCorners(CubieCube other)
    {
        Span<byte> cp = stackalloc byte[8];
        Span<byte> co = stackalloc byte[8];
        for (var i = 0; i < 8; i++)
        {
            cp[i] = Cp[other.Cp[i]];
            co[i] = (byte)((Co[other.Cp[i]] + other.Co[i]) % 3);
        }
        cp.CopyTo(Cp);
        co.CopyTo(Co);
    }

    public void MultiplyEdges(CubieCube other)
    {
        Span<byte> ep = stackalloc byte[12];
        Span<byte> eo = stackalloc byte[12];
        for (var i = 0; i < 12; i++)
        {
            ep[i] = Ep[other.Ep[i]];
            eo[i] = (byte)((Eo[other.Ep[i]] + other.Eo[i]) & 1);
        }
        ep.CopyTo(Ep);
        eo.CopyTo(Eo);
    }

    public CubieCube Inverse()
    {
        var result = new CubieCube();
        for (var i = 0; i < 8; i++)
        {
            result.Cp[Cp[i]] = (byte)i;
        }
        for (var i = 0; i < 8; i++)
        {
            result.Co[i] = (byte)((3 - Co[result.Cp[i]]) % 3);
        }
        for (var i = 0; i < 12; i++)
        {
            result.Ep[Ep[i]] = (byte)i;
        }
        for (var i = 0; i < 12; i++)
        {
            result.Eo[i] = Eo[result.Ep[i]];
        }
        return result;
    }

    /// <summary>Applies a face move in place. Wide moves, slices and rotations are not supported here.</summary>
    public void Apply(Move move)
    {
        if (!move.IsFaceMove)
        {
            throw new ArgumentException($"{move} is not a face move; use CubeState for the full notation.", nameof(move));
        }
        var cube = MoveCubes[(int)move.Face];
        for (var i = 0; i < (int)move.Turn; i++)
        {
            Multiply(cube);
        }
    }

    public void Apply(Algorithm algorithm)
    {
        foreach (var move in algorithm.Moves)
        {
            Apply(move);
        }
    }

    public CubeValidity Validate()
    {
        Span<bool> seen = stackalloc bool[12];
        for (var i = 0; i < 8; i++)
        {
            if (Cp[i] > 7 || seen[Cp[i]])
            {
                return CubeValidity.DuplicatePiece;
            }
            seen[Cp[i]] = true;
        }
        seen.Clear();
        for (var i = 0; i < 12; i++)
        {
            if (Ep[i] > 11 || seen[Ep[i]])
            {
                return CubeValidity.DuplicatePiece;
            }
            seen[Ep[i]] = true;
        }

        var twist = 0;
        for (var i = 0; i < 8; i++)
        {
            twist += Co[i];
        }
        if (twist % 3 != 0)
        {
            return CubeValidity.CornerTwist;
        }

        var flip = 0;
        for (var i = 0; i < 12; i++)
        {
            flip += Eo[i];
        }
        if (flip % 2 != 0)
        {
            return CubeValidity.EdgeFlip;
        }

        return CornerParity == EdgeParity ? CubeValidity.Valid : CubeValidity.PermutationParity;
    }

    /// <summary>Parity of the corner permutation: 0 even, 1 odd.</summary>
    public int CornerParity => Parity(Cp);

    /// <summary>Parity of the edge permutation: 0 even, 1 odd.</summary>
    public int EdgeParity => Parity(Ep);

    private static int Parity(byte[] perm)
    {
        var parity = 0;
        for (var i = perm.Length - 1; i > 0; i--)
        {
            for (var j = i - 1; j >= 0; j--)
            {
                if (perm[j] > perm[i])
                {
                    parity ^= 1;
                }
            }
        }
        return parity;
    }

    public Face[] ToFacelets()
    {
        var f = new Face[CubeState.FaceletCount];
        for (var face = 0; face < 6; face++)
        {
            f[face * 9 + 4] = (Face)face;
        }
        for (var slot = 0; slot < 8; slot++)
        {
            var piece = Cp[slot];
            var ori = Co[slot];
            for (var n = 0; n < 3; n++)
            {
                f[CubeDefinitions.CornerFacelets[slot][(n + ori) % 3]] = CubeDefinitions.CornerColors[piece][n];
            }
        }
        for (var slot = 0; slot < 12; slot++)
        {
            var piece = Ep[slot];
            var ori = Eo[slot];
            for (var n = 0; n < 2; n++)
            {
                f[CubeDefinitions.EdgeFacelets[slot][(n + ori) % 2]] = CubeDefinitions.EdgeColors[piece][n];
            }
        }
        return f;
    }

    /// <summary>Builds a cubie cube from facelets that are already in the home orientation.</summary>
    public static CubieCube FromFacelets(ReadOnlySpan<Face> f)
    {
        if (f.Length != CubeState.FaceletCount)
        {
            throw new ArgumentException("Expected 54 facelets.", nameof(f));
        }
        var cube = new CubieCube();
        Array.Fill(cube.Cp, (byte)255);
        Array.Fill(cube.Ep, (byte)255);

        for (var slot = 0; slot < 8; slot++)
        {
            var facelets = CubeDefinitions.CornerFacelets[slot];
            var ori = 0;
            while (ori < 3 && f[facelets[ori]] != Face.U && f[facelets[ori]] != Face.D)
            {
                ori++;
            }
            if (ori == 3)
            {
                throw new InvalidCubeException($"Corner slot {(Corner)slot} has no U or D sticker.");
            }
            var col1 = f[facelets[(ori + 1) % 3]];
            var col2 = f[facelets[(ori + 2) % 3]];
            var piece = -1;
            for (var j = 0; j < 8; j++)
            {
                if (CubeDefinitions.CornerColors[j][1] == col1 && CubeDefinitions.CornerColors[j][2] == col2)
                {
                    piece = j;
                    break;
                }
            }
            if (piece < 0)
            {
                throw new InvalidCubeException($"Corner slot {(Corner)slot} holds an impossible colour combination.");
            }
            cube.Cp[slot] = (byte)piece;
            cube.Co[slot] = (byte)ori;
        }

        for (var slot = 0; slot < 12; slot++)
        {
            var facelets = CubeDefinitions.EdgeFacelets[slot];
            var a = f[facelets[0]];
            var b = f[facelets[1]];
            var found = false;
            for (var j = 0; j < 12 && !found; j++)
            {
                var colors = CubeDefinitions.EdgeColors[j];
                if (colors[0] == a && colors[1] == b)
                {
                    cube.Ep[slot] = (byte)j;
                    cube.Eo[slot] = 0;
                    found = true;
                }
                else if (colors[0] == b && colors[1] == a)
                {
                    cube.Ep[slot] = (byte)j;
                    cube.Eo[slot] = 1;
                    found = true;
                }
            }
            if (!found)
            {
                throw new InvalidCubeException($"Edge slot {(Edge)slot} holds an impossible colour combination.");
            }
        }

        var validity = cube.Validate();
        return validity == CubeValidity.Valid
            ? cube
            : throw new InvalidCubeException($"The cube is not solvable: {validity}.");
    }

    private static CubieCube[] BuildMoveCubes()
    {
        var result = new CubieCube[6];
        for (var face = 0; face < 6; face++)
        {
            var state = CubeState.Solved.Apply(Move.Face_((Face)face));
            result[face] = FromFacelets(state.Facelets);
        }
        return result;
    }

    public bool Equals(CubieCube? other) =>
        other is not null
        && Cp.AsSpan().SequenceEqual(other.Cp)
        && Co.AsSpan().SequenceEqual(other.Co)
        && Ep.AsSpan().SequenceEqual(other.Ep)
        && Eo.AsSpan().SequenceEqual(other.Eo);

    public override bool Equals(object? obj) => Equals(obj as CubieCube);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(Cp);
        hash.AddBytes(Co);
        hash.AddBytes(Ep);
        hash.AddBytes(Eo);
        return hash.ToHashCode();
    }
}
