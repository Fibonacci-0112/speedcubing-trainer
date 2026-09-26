using System.Text;

namespace SpeedcubingTrainer.Core.Cube;

/// <summary>Thrown when a facelet configuration is not a reachable cube state.</summary>
public sealed class InvalidCubeException(string message) : InvalidOperationException(message);

/// <summary>
/// Immutable 54-sticker model of the cube. Facelets are ordered U1..U9 R1..R9 F1..F9 D1..D9 L1..L9 B1..B9,
/// each face read row by row as drawn in the standard net. Supports the full notation, including
/// wide moves, slices and rotations; centres move with rotations.
/// </summary>
public sealed class CubeState : IEquatable<CubeState>
{
    public const int FaceletCount = CubeGeometry.FaceletCount;

    private static readonly int[] CenterIndices = [4, 13, 22, 31, 40, 49];

    public static readonly CubeState Solved = new(Enumerable.Range(0, FaceletCount).Select(i => (Face)(i / 9)).ToArray());

    private readonly Face[] _facelets;

    private CubeState(Face[] facelets)
    {
        _facelets = facelets;
    }

    public Face this[int index] => _facelets[index];

    public ReadOnlySpan<Face> Facelets => _facelets;

    /// <summary>The nine stickers of a face in net order.</summary>
    public ReadOnlySpan<Face> GetFace(Face face) => _facelets.AsSpan((int)face * 9, 9);

    public bool IsSolved
    {
        get
        {
            for (var face = 0; face < 6; face++)
            {
                var centre = _facelets[face * 9 + 4];
                for (var i = 0; i < 9; i++)
                {
                    if (_facelets[face * 9 + i] != centre)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }

    /// <summary>True when every centre is on its home face (white on top, green in front).</summary>
    public bool HasHomeOrientation
    {
        get
        {
            for (var face = 0; face < 6; face++)
            {
                if (_facelets[CenterIndices[face]] != (Face)face)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public CubeState Apply(Move move)
    {
        var perm = CubeGeometry.Permutation(move);
        var result = new Face[FaceletCount];
        for (var i = 0; i < FaceletCount; i++)
        {
            result[i] = _facelets[perm[i]];
        }
        return new CubeState(result);
    }

    public CubeState Apply(Algorithm algorithm)
    {
        var state = this;
        foreach (var move in algorithm.Moves)
        {
            state = state.Apply(move);
        }
        return state;
    }

    public CubeState Apply(string algorithm) => Apply(Algorithm.Parse(algorithm));

    /// <summary>Rotates the whole cube so that every centre is on its home face.</summary>
    public CubeState NormalizeOrientation()
    {
        if (HasHomeOrientation)
        {
            return this;
        }
        foreach (var orientation in CubeGeometry.Orientations)
        {
            var ok = true;
            for (var face = 0; face < 6 && ok; face++)
            {
                ok = _facelets[orientation[CenterIndices[face]]] == (Face)face;
            }
            if (ok)
            {
                var result = new Face[FaceletCount];
                for (var i = 0; i < FaceletCount; i++)
                {
                    result[i] = _facelets[orientation[i]];
                }
                return new CubeState(result);
            }
        }
        throw new InvalidCubeException("The centres do not form a valid colour scheme.");
    }

    /// <summary>
    /// Rotates the cube (with x or z rotations only) so that the U centre is on top, keeping any y rotation.
    /// This is how a cuber holds the cube after an algorithm that ends in a y rotation.
    /// </summary>
    public CubeState NormalizeUpFace()
    {
        var upCentreLocation = 0;
        for (var face = 0; face < 6; face++)
        {
            if (_facelets[CenterIndices[face]] == Face.U)
            {
                upCentreLocation = face;
                break;
            }
        }
        return (Face)upCentreLocation switch
        {
            Face.U => this,
            Face.D => Apply(new Move(MoveTarget.X, Turn.Double)),
            Face.F => Apply(new Move(MoveTarget.X, Turn.Cw)),
            Face.B => Apply(new Move(MoveTarget.X, Turn.Ccw)),
            Face.R => Apply(new Move(MoveTarget.Z, Turn.Ccw)),
            _ => Apply(new Move(MoveTarget.Z, Turn.Cw)),
        };
    }

    /// <summary>
    /// Relabels colours so that every centre shows its home colour, without moving any sticker.
    /// Used to present a case in the standard colour scheme regardless of how the cube is turned.
    /// </summary>
    public CubeState RecolorToHome()
    {
        if (HasHomeOrientation)
        {
            return this;
        }
        var map = new Face[6];
        for (var face = 0; face < 6; face++)
        {
            map[(int)_facelets[CenterIndices[face]]] = (Face)face;
        }
        var result = new Face[FaceletCount];
        for (var i = 0; i < FaceletCount; i++)
        {
            result[i] = map[(int)_facelets[i]];
        }
        return new CubeState(result);
    }

    /// <summary>Converts to the cubie model. The cube is first rotated to its home orientation.</summary>
    public CubieCube ToCubieCube() => CubieCube.FromFacelets(NormalizeOrientation()._facelets);

    public static CubeState FromCubieCube(in CubieCube cube) => new(cube.ToFacelets());

    internal static CubeState FromFaceletsUnchecked(Face[] facelets) => new(facelets);

    /// <summary>Parses a 54-character string of U R F D L B letters in facelet order.</summary>
    public static CubeState Parse(string facelets)
    {
        ArgumentNullException.ThrowIfNull(facelets);
        var trimmed = facelets.Replace(" ", string.Empty);
        if (trimmed.Length != FaceletCount)
        {
            throw new FormatException($"Expected {FaceletCount} facelets, got {trimmed.Length}.");
        }
        var result = new Face[FaceletCount];
        for (var i = 0; i < FaceletCount; i++)
        {
            result[i] = trimmed[i] switch
            {
                'U' => Face.U,
                'R' => Face.R,
                'F' => Face.F,
                'D' => Face.D,
                'L' => Face.L,
                'B' => Face.B,
                var c => throw new FormatException($"Invalid facelet '{c}' at index {i}."),
            };
        }
        return new CubeState(result);
    }

    public override string ToString()
    {
        var sb = new StringBuilder(FaceletCount);
        foreach (var f in _facelets)
        {
            sb.Append(f.ToString());
        }
        return sb.ToString();
    }

    public bool Equals(CubeState? other) => other is not null && _facelets.AsSpan().SequenceEqual(other._facelets);

    public override bool Equals(object? obj) => Equals(obj as CubeState);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(System.Runtime.InteropServices.MemoryMarshal.AsBytes(_facelets.AsSpan()));
        return hash.ToHashCode();
    }
}
