using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Tests.Cube;

public class CubieCubeTests
{
    [Theory]
    [InlineData(Face.U)]
    [InlineData(Face.R)]
    [InlineData(Face.F)]
    [InlineData(Face.D)]
    [InlineData(Face.L)]
    [InlineData(Face.B)]
    public void FaceMoveFourTimesIsIdentity(Face face)
    {
        var cube = CubieCube.Solved;
        var move = Move.Face_(face);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(i == 0, cube.IsSolved);
            cube.Apply(move);
        }
        Assert.True(cube.IsSolved);
    }

    [Theory]
    [InlineData("R U R' U'", 6)]
    [InlineData("R U R' U R U2 R'", 6)]
    [InlineData("R U R' U' R' F R2 U' R' U' R U R' F'", 2)]
    [InlineData("F R U R' U' F'", 6)]
    [InlineData("R2 U R U R' U' R' U' R' U R'", 3)]
    public void KnownAlgorithmOrders(string algorithm, int order)
    {
        var alg = Algorithm.Parse(algorithm);
        var cube = CubieCube.Solved;
        for (var i = 1; i <= order; i++)
        {
            cube.Apply(alg);
            Assert.Equal(i == order, cube.IsSolved);
        }
    }

    [Fact]
    public void TPermHasOddParityInBothPermutations()
    {
        var cube = CubieCube.Solved;
        cube.Apply(Algorithm.Parse("R U R' U' R' F R2 U' R' U' R U R' F'"));
        Assert.Equal(1, cube.CornerParity);
        Assert.Equal(1, cube.EdgeParity);
        Assert.Equal(CubeValidity.Valid, cube.Validate());
    }

    [Fact]
    public void FaceletAndCubieModelsAgreeOnEveryMoveSequence()
    {
        var rng = new Random(12345);
        var faces = Enum.GetValues<Face>();
        for (var trial = 0; trial < 200; trial++)
        {
            var state = CubeState.Solved;
            var cube = CubieCube.Solved;
            var length = rng.Next(1, 30);
            for (var i = 0; i < length; i++)
            {
                var move = Move.Face_(faces[rng.Next(6)], (Turn)rng.Next(1, 4));
                state = state.Apply(move);
                cube.Apply(move);
            }
            Assert.Equal(state, CubeState.FromCubieCube(cube));
            Assert.Equal(cube, state.ToCubieCube());
        }
    }

    [Fact]
    public void RoundTripOfRandomValidCubes()
    {
        var rng = new Random(7);
        for (var trial = 0; trial < 1000; trial++)
        {
            var cube = RandomValidCube(rng);
            Assert.Equal(CubeValidity.Valid, cube.Validate());
            var facelets = CubeState.FromCubieCube(cube);
            Assert.Equal(cube, facelets.ToCubieCube());
        }
    }

    [Fact]
    public void InverseComposesToIdentity()
    {
        var rng = new Random(99);
        for (var trial = 0; trial < 100; trial++)
        {
            var cube = RandomValidCube(rng);
            var product = cube.Clone();
            product.Multiply(cube.Inverse());
            Assert.True(product.IsSolved);
            var product2 = cube.Inverse();
            product2.Multiply(cube);
            Assert.True(product2.IsSolved);
        }
    }

    [Fact]
    public void MultiplyMatchesSequentialApplication()
    {
        var a = Algorithm.Parse("R U2 F' D L B2");
        var b = Algorithm.Parse("U' L2 F R' D2 B");
        var sequential = CubieCube.Solved;
        sequential.Apply(a);
        sequential.Apply(b);
        var cubeA = CubieCube.Solved;
        cubeA.Apply(a);
        var cubeB = CubieCube.Solved;
        cubeB.Apply(b);
        cubeA.Multiply(cubeB);
        Assert.Equal(sequential, cubeA);
    }

    [Fact]
    public void ValidateDetectsImpossibleCubes()
    {
        var twisted = CubieCube.Solved;
        twisted.Co[0] = 1;
        Assert.Equal(CubeValidity.CornerTwist, twisted.Validate());

        var flipped = CubieCube.Solved;
        flipped.Eo[3] = 1;
        Assert.Equal(CubeValidity.EdgeFlip, flipped.Validate());

        var parity = CubieCube.Solved;
        (parity.Cp[0], parity.Cp[1]) = (parity.Cp[1], parity.Cp[0]);
        Assert.Equal(CubeValidity.PermutationParity, parity.Validate());

        var duplicate = CubieCube.Solved;
        duplicate.Ep[0] = duplicate.Ep[1];
        Assert.Equal(CubeValidity.DuplicatePiece, duplicate.Validate());

        Assert.Throws<InvalidCubeException>(() => CubeState.FromCubieCube(twisted).ToCubieCube());
    }

    internal static CubieCube RandomValidCube(Random rng)
    {
        var cube = CubieCube.Solved;
        Shuffle(cube.Cp, rng);
        Shuffle(cube.Ep, rng);
        if (cube.CornerParity != cube.EdgeParity)
        {
            (cube.Ep[0], cube.Ep[1]) = (cube.Ep[1], cube.Ep[0]);
        }
        var twist = 0;
        for (var i = 0; i < 7; i++)
        {
            cube.Co[i] = (byte)rng.Next(3);
            twist += cube.Co[i];
        }
        cube.Co[7] = (byte)((3 - twist % 3) % 3);
        var flip = 0;
        for (var i = 0; i < 11; i++)
        {
            cube.Eo[i] = (byte)rng.Next(2);
            flip += cube.Eo[i];
        }
        cube.Eo[11] = (byte)(flip & 1);
        return cube;
    }

    private static void Shuffle(byte[] items, Random rng)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
