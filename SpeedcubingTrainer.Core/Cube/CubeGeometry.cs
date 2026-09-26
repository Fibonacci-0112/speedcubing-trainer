namespace SpeedcubingTrainer.Core.Cube;

/// <summary>
/// Derives every facelet permutation from the cube's geometry: each of the 54 stickers has a
/// cubie position and an outward normal, and a move is a quarter-turn rotation of a set of layers.
/// Nothing about individual moves is hand-coded, which keeps the facelet model free of typos.
/// </summary>
internal static class CubeGeometry
{
    public const int FaceletCount = 54;

    private readonly record struct Vec(int X, int Y, int Z)
    {
        public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec operator *(int k, Vec a) => new(k * a.X, k * a.Y, k * a.Z);
        public int Dot(Vec o) => X * o.X + Y * o.Y + Z * o.Z;
        public Vec Cross(Vec o) => new(Y * o.Z - Z * o.Y, Z * o.X - X * o.Z, X * o.Y - Y * o.X);
    }

    private readonly record struct Facelet(Vec Position, Vec Normal);

    private static readonly Vec[] Normals =
    [
        new(0, 1, 0),  // U
        new(1, 0, 0),  // R
        new(0, 0, 1),  // F
        new(0, -1, 0), // D
        new(-1, 0, 0), // L
        new(0, 0, -1), // B
    ];

    // Top-left cubie, "right" direction and "down" direction of each face as drawn in the
    // standard net (U seen from above with B at the top, D seen from below with F at the top).
    private static readonly (Vec Origin, Vec Right, Vec Down)[] Layout =
    [
        (new(-1, 1, -1), new(1, 0, 0), new(0, 0, 1)),  // U
        (new(1, 1, 1), new(0, 0, -1), new(0, -1, 0)),  // R
        (new(-1, 1, 1), new(1, 0, 0), new(0, -1, 0)),  // F
        (new(-1, -1, 1), new(1, 0, 0), new(0, 0, -1)), // D
        (new(-1, 1, -1), new(0, 0, 1), new(0, -1, 0)), // L
        (new(1, 1, -1), new(-1, 0, 0), new(0, -1, 0)), // B
    ];

    private static readonly Facelet[] Facelets = BuildFacelets();
    private static readonly Dictionary<Facelet, int> FaceletIndex = Facelets.Select((f, i) => (f, i)).ToDictionary(p => p.f, p => p.i);

    /// <summary>Permutation for each (target, turn): result[i] = source index of the sticker that lands on i.</summary>
    private static readonly byte[][] Permutations = BuildPermutations();

    /// <summary>The 24 whole-cube orientations as facelet permutations, identity first.</summary>
    public static readonly byte[][] Orientations = BuildOrientations();

    public static ReadOnlySpan<byte> Permutation(Move move) => Permutations[Index(move)];

    private static int Index(Move move) => (int)move.Target * 3 + ((int)move.Turn - 1);

    /// <summary>Position (0..8) of every facelet inside its face, row-major as drawn in the net.</summary>
    public static int CellOf(int facelet) => facelet % 9;

    public static Face FaceOf(int facelet) => (Face)(facelet / 9);

    /// <summary>True when the three (or two) facelets share the same cubie position.</summary>
    internal static bool SameCubie(int a, int b) => Facelets[a].Position == Facelets[b].Position;

    internal static bool IsUpOrDown(int facelet) => Facelets[facelet].Normal.Y != 0;

    private static Facelet[] BuildFacelets()
    {
        var result = new Facelet[FaceletCount];
        for (var face = 0; face < 6; face++)
        {
            var (origin, right, down) = Layout[face];
            for (var row = 0; row < 3; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    result[face * 9 + row * 3 + col] = new Facelet(origin + col * right + row * down, Normals[face]);
                }
            }
        }
        return result;
    }

    /// <summary>Rotation by -90 degrees about a unit axis: clockwise when looking at the face the axis points to.</summary>
    private static Vec RotateClockwise(Vec axis, Vec v) => axis.Dot(v) * axis - axis.Cross(v);

    private static (Vec Axis, int[] Layers) Describe(MoveTarget target) => target switch
    {
        MoveTarget.U => (Normals[0], [1]),
        MoveTarget.R => (Normals[1], [1]),
        MoveTarget.F => (Normals[2], [1]),
        MoveTarget.D => (Normals[3], [1]),
        MoveTarget.L => (Normals[4], [1]),
        MoveTarget.B => (Normals[5], [1]),
        MoveTarget.Uw => (Normals[0], [0, 1]),
        MoveTarget.Rw => (Normals[1], [0, 1]),
        MoveTarget.Fw => (Normals[2], [0, 1]),
        MoveTarget.Dw => (Normals[3], [0, 1]),
        MoveTarget.Lw => (Normals[4], [0, 1]),
        MoveTarget.Bw => (Normals[5], [0, 1]),
        MoveTarget.M => (Normals[4], [0]), // follows L
        MoveTarget.E => (Normals[3], [0]), // follows D
        MoveTarget.S => (Normals[2], [0]), // follows F
        MoveTarget.X => (Normals[1], [-1, 0, 1]),
        MoveTarget.Y => (Normals[0], [-1, 0, 1]),
        MoveTarget.Z => (Normals[2], [-1, 0, 1]),
        _ => throw new ArgumentOutOfRangeException(nameof(target)),
    };

    private static byte[] QuarterTurn(MoveTarget target)
    {
        var (axis, layers) = Describe(target);
        var perm = new byte[FaceletCount];
        for (var i = 0; i < FaceletCount; i++)
        {
            var f = Facelets[i];
            if (Array.IndexOf(layers, axis.Dot(f.Position)) < 0)
            {
                perm[i] = (byte)i;
                continue;
            }
            var moved = new Facelet(RotateClockwise(axis, f.Position), RotateClockwise(axis, f.Normal));
            perm[FaceletIndex[moved]] = (byte)i;
        }
        return perm;
    }

    internal static byte[] Compose(byte[] first, byte[] second)
    {
        // Apply "first" then "second": result[i] = first[second[i]].
        var result = new byte[FaceletCount];
        for (var i = 0; i < FaceletCount; i++)
        {
            result[i] = first[second[i]];
        }
        return result;
    }

    private static byte[][] BuildPermutations()
    {
        var targets = Enum.GetValues<MoveTarget>();
        var result = new byte[targets.Length * 3][];
        foreach (var target in targets)
        {
            var single = QuarterTurn(target);
            var twice = Compose(single, single);
            result[(int)target * 3 + 0] = single;
            result[(int)target * 3 + 1] = twice;
            result[(int)target * 3 + 2] = Compose(twice, single);
        }
        return result;
    }

    private static byte[][] BuildOrientations()
    {
        var identity = Enumerable.Range(0, FaceletCount).Select(i => (byte)i).ToArray();
        var seen = new List<byte[]> { identity };
        var queue = new Queue<byte[]>();
        queue.Enqueue(identity);
        Move[] generators = [new(MoveTarget.X, Turn.Cw), new(MoveTarget.Y, Turn.Cw), new(MoveTarget.Z, Turn.Cw)];
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var g in generators)
            {
                var next = Compose(current, Permutations[Index(g)]);
                if (!seen.Any(s => s.AsSpan().SequenceEqual(next)))
                {
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }
        }
        return [.. seen];
    }
}
