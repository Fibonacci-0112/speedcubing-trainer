namespace SpeedcubingTrainer.Core.Cube;

/// <summary>The six faces of the cube. The value doubles as the sticker colour of that face's centre.</summary>
public enum Face : byte
{
    U = 0,
    R = 1,
    F = 2,
    D = 3,
    L = 4,
    B = 5,
}

/// <summary>Rotation axis of a move.</summary>
public enum Axis : byte
{
    X, // R, L, M, x
    Y, // U, D, E, y
    Z, // F, B, S, z
}

/// <summary>Everything a move can turn: a face, a wide (two-layer) block, a middle slice or the whole cube.</summary>
public enum MoveTarget : byte
{
    U, R, F, D, L, B,
    Uw, Rw, Fw, Dw, Lw, Bw,
    M, E, S,
    X, Y, Z,
}

/// <summary>Amount of a turn, in quarter turns clockwise.</summary>
public enum Turn : byte
{
    Cw = 1,
    Double = 2,
    Ccw = 3,
}

/// <summary>Plane used when mirroring an algorithm.</summary>
public enum MirrorPlane
{
    /// <summary>Mirror across the M slice: R and L swap.</summary>
    LeftRight,
    /// <summary>Mirror across the S slice: F and B swap.</summary>
    FrontBack,
}

/// <summary>How wide moves are written.</summary>
public enum NotationStyle
{
    /// <summary>Rw, Uw, ...</summary>
    Wide,
    /// <summary>r, u, ...</summary>
    Lowercase,
}

/// <summary>A single move in standard 3x3 notation.</summary>
public readonly record struct Move(MoveTarget Target, Turn Turn)
{
    public Move Inverse => new(Target, (Turn)(4 - (int)Turn));

    public bool IsFaceMove => Target <= MoveTarget.B;

    public bool IsWide => Target is >= MoveTarget.Uw and <= MoveTarget.Bw;

    public bool IsSlice => Target is MoveTarget.M or MoveTarget.E or MoveTarget.S;

    public bool IsRotation => Target is MoveTarget.X or MoveTarget.Y or MoveTarget.Z;

    /// <summary>The face turned by a face move. Throws for anything else.</summary>
    public Face Face => IsFaceMove
        ? (Face)(byte)Target
        : throw new InvalidOperationException($"{this} is not a face move.");

    public Axis Axis => Target switch
    {
        MoveTarget.R or MoveTarget.L or MoveTarget.Rw or MoveTarget.Lw or MoveTarget.M or MoveTarget.X => Axis.X,
        MoveTarget.U or MoveTarget.D or MoveTarget.Uw or MoveTarget.Dw or MoveTarget.E or MoveTarget.Y => Axis.Y,
        _ => Axis.Z,
    };

    public Move Mirror(MirrorPlane plane)
    {
        var target = plane switch
        {
            MirrorPlane.LeftRight => Target switch
            {
                MoveTarget.R => MoveTarget.L,
                MoveTarget.L => MoveTarget.R,
                MoveTarget.Rw => MoveTarget.Lw,
                MoveTarget.Lw => MoveTarget.Rw,
                _ => Target,
            },
            _ => Target switch
            {
                MoveTarget.F => MoveTarget.B,
                MoveTarget.B => MoveTarget.F,
                MoveTarget.Fw => MoveTarget.Bw,
                MoveTarget.Bw => MoveTarget.Fw,
                _ => Target,
            },
        };
        return new Move(target, (Turn)(4 - (int)Turn));
    }

    public static Move Face_(Face face, Turn turn = Turn.Cw) => new((MoveTarget)(byte)face, turn);

    public override string ToString() => ToString(NotationStyle.Wide);

    public string ToString(NotationStyle style)
    {
        var name = Target switch
        {
            MoveTarget.Uw => style == NotationStyle.Wide ? "Uw" : "u",
            MoveTarget.Rw => style == NotationStyle.Wide ? "Rw" : "r",
            MoveTarget.Fw => style == NotationStyle.Wide ? "Fw" : "f",
            MoveTarget.Dw => style == NotationStyle.Wide ? "Dw" : "d",
            MoveTarget.Lw => style == NotationStyle.Wide ? "Lw" : "l",
            MoveTarget.Bw => style == NotationStyle.Wide ? "Bw" : "b",
            MoveTarget.X => "x",
            MoveTarget.Y => "y",
            MoveTarget.Z => "z",
            _ => Target.ToString(),
        };
        return Turn switch
        {
            Turn.Cw => name,
            Turn.Double => name + "2",
            _ => name + "'",
        };
    }
}
