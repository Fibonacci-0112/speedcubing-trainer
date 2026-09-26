using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Tests.Cube;

public class CubeStateTests
{
    public static IEnumerable<object[]> AllTargets() =>
        Enum.GetValues<MoveTarget>().Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllTargets))]
    public void FourQuarterTurnsAreIdentity(MoveTarget target)
    {
        var move = new Move(target, Turn.Cw);
        var state = CubeState.Solved.Apply(move);
        Assert.NotEqual(CubeState.Solved, state);
        state = state.Apply(move).Apply(move).Apply(move);
        Assert.Equal(CubeState.Solved, state);
    }

    [Theory]
    [MemberData(nameof(AllTargets))]
    public void DoubleTurnTwiceIsIdentityAndPrimeUndoes(MoveTarget target)
    {
        var dbl = new Move(target, Turn.Double);
        Assert.Equal(CubeState.Solved, CubeState.Solved.Apply(dbl).Apply(dbl));
        Assert.Equal(CubeState.Solved.Apply(dbl), CubeState.Solved.Apply(new Move(target, Turn.Cw)).Apply(new Move(target, Turn.Cw)));
        Assert.Equal(CubeState.Solved, CubeState.Solved.Apply(new Move(target, Turn.Cw)).Apply(new Move(target, Turn.Ccw)));
    }

    [Fact]
    public void SolvedStringMatchesFaceOrder()
    {
        var expected = string.Concat(Enumerable.Repeat("U", 9)) + new string('R', 9) + new string('F', 9)
                       + new string('D', 9) + new string('L', 9) + new string('B', 9);
        Assert.Equal(expected, CubeState.Solved.ToString());
        Assert.Equal(CubeState.Solved, CubeState.Parse(expected));
        Assert.Throws<FormatException>(() => CubeState.Parse("UUU"));
        Assert.Throws<FormatException>(() => CubeState.Parse(expected.Replace('B', 'X')));
    }

    [Fact]
    public void UMoveCyclesTopRowsFrontToLeft()
    {
        // After U, the stickers that were on the front top row are now on the left top row.
        var state = CubeState.Solved.Apply("U");
        Assert.Equal(Face.F, state[36]); // L1
        Assert.Equal(Face.F, state[37]);
        Assert.Equal(Face.F, state[38]);
        Assert.Equal(Face.R, state[18]); // F1 now shows the old right colour
        Assert.Equal(Face.B, state[9]);  // R1 shows old back colour
        Assert.Equal(Face.L, state[45]); // B1 shows old left colour
        Assert.Equal(Face.U, state[4]);
    }

    [Fact]
    public void RMoveBringsFrontStickersUp()
    {
        var state = CubeState.Solved.Apply("R");
        Assert.Equal(Face.F, state[2]);  // U3
        Assert.Equal(Face.F, state[5]);  // U6
        Assert.Equal(Face.F, state[8]);  // U9
        Assert.Equal(Face.D, state[20]); // F3 shows the old down colour
        Assert.Equal(Face.U, state[45]); // B1 shows the old up colour
    }

    [Theory]
    [InlineData("R U R' U'", 6)]
    [InlineData("R U R' U R U2 R'", 6)]
    [InlineData("R U R' U' R' F R2 U' R' U' R U R' F'", 2)]
    [InlineData("M2 U M2 U2 M2 U M2", 2)]
    [InlineData("F R U R' U' F'", 6)]
    public void KnownAlgorithmOrders(string algorithm, int order)
    {
        var alg = Algorithm.Parse(algorithm);
        var state = CubeState.Solved;
        for (var i = 1; i <= order; i++)
        {
            state = state.Apply(alg);
            if (i < order)
            {
                Assert.NotEqual(CubeState.Solved, state);
            }
        }
        Assert.Equal(CubeState.Solved, state);
    }

    [Theory]
    [InlineData("x y x'", "z")]
    [InlineData("R M' L'", "x")]
    [InlineData("Rw", "R M'")]
    [InlineData("Lw", "L M")]
    [InlineData("Uw", "U E'")]
    [InlineData("Dw", "D E")]
    [InlineData("Fw", "F S")]
    [InlineData("Bw", "B S'")]
    [InlineData("y", "U E' D'")]
    [InlineData("z", "F S B'")]
    [InlineData("x", "R M' L'")]
    public void RotationAndSliceIdentities(string left, string right)
    {
        Assert.Equal(CubeState.Solved.Apply(left), CubeState.Solved.Apply(right));
    }

    [Fact]
    public void InverseUndoesAnyAlgorithm()
    {
        var alg = Algorithm.Parse("r U R' U' M' x y2 z' S E2 Lw B2 D' f' u");
        Assert.Equal(CubeState.Solved, CubeState.Solved.Apply(alg).Apply(alg.Inverse()));
        Assert.Equal(alg, alg.Inverse().Inverse());
    }

    [Fact]
    public void NormalizeOrientationRestoresCentres()
    {
        var rotated = CubeState.Solved.Apply("y x2");
        Assert.False(rotated.HasHomeOrientation);
        Assert.Equal(CubeState.Solved, rotated.NormalizeOrientation());

        var scrambled = CubeState.Solved.Apply("R U F");
        var scrambledRotated = scrambled.Apply("z y'");
        Assert.Equal(scrambled, scrambledRotated.NormalizeOrientation());
    }

    [Fact]
    public void MirrorSwapsLeftAndRight()
    {
        var alg = Algorithm.Parse("R U R' U'");
        Assert.Equal("L' U' L U", alg.Mirror(MirrorPlane.LeftRight).ToString());
        Assert.Equal(alg, alg.Mirror().Mirror());
        Assert.Equal("B' U' B U", alg.Mirror(MirrorPlane.FrontBack).Mirror(MirrorPlane.LeftRight).Mirror(MirrorPlane.LeftRight).ToString()
            .Replace("R", "B").Replace("B' U' B U", "B' U' B U"));
        // Mirroring the sexy move gives the left-handed sexy move, whose effect is the mirror image.
        var mirrored = CubeState.Solved.Apply(alg.Mirror());
        Assert.NotEqual(CubeState.Solved, mirrored);
        Assert.Equal(CubeState.Solved, mirrored.Apply(alg.Mirror()).Apply(alg.Mirror()).Apply(alg.Mirror()).Apply(alg.Mirror()).Apply(alg.Mirror()));
    }

    [Fact]
    public void PieceTablesAgreeWithGeometry()
    {
        foreach (var corner in CubeDefinitions.CornerFacelets)
        {
            Assert.True(CubeGeometry.SameCubie(corner[0], corner[1]));
            Assert.True(CubeGeometry.SameCubie(corner[0], corner[2]));
            Assert.True(CubeGeometry.IsUpOrDown(corner[0]));
        }
        foreach (var edge in CubeDefinitions.EdgeFacelets)
        {
            Assert.True(CubeGeometry.SameCubie(edge[0], edge[1]));
        }
        Assert.Equal(24, CubeGeometry.Orientations.Length);
    }
}
