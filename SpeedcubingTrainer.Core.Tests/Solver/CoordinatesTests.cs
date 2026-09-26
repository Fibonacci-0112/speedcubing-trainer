using SpeedcubingTrainer.Core.Cube;
using SpeedcubingTrainer.Core.Solver;
using SpeedcubingTrainer.Core.Tests.Cube;

namespace SpeedcubingTrainer.Core.Tests.Solver;

public class CoordinatesTests
{
    [Fact]
    public void SolvedCubeHasZeroCoordinates()
    {
        var cube = CubieCube.Solved;
        Assert.Equal(0, Coordinates.GetTwist(cube));
        Assert.Equal(0, Coordinates.GetFlip(cube));
        Assert.Equal(0, Coordinates.GetSlice(cube));
        Assert.Equal(0, Coordinates.GetCornerPerm(cube));
        Assert.Equal(0, Coordinates.GetUdEdgePerm(cube));
        Assert.Equal(0, Coordinates.GetSlicePerm(cube));
    }

    [Fact]
    public void TwistRoundTrips()
    {
        var cube = CubieCube.Solved;
        for (var t = 0; t < Coordinates.TwistCount; t++)
        {
            Coordinates.SetTwist(cube, t);
            Assert.Equal(t, Coordinates.GetTwist(cube));
            Assert.Equal(0, cube.Co.Sum(c => c) % 3);
        }
    }

    [Fact]
    public void FlipRoundTrips()
    {
        var cube = CubieCube.Solved;
        for (var f = 0; f < Coordinates.FlipCount; f++)
        {
            Coordinates.SetFlip(cube, f);
            Assert.Equal(f, Coordinates.GetFlip(cube));
            Assert.Equal(0, cube.Eo.Sum(e => e) % 2);
        }
    }

    [Fact]
    public void SliceRoundTripsAndIsUnique()
    {
        var cube = CubieCube.Solved;
        var seen = new HashSet<string>();
        for (var s = 0; s < Coordinates.SliceCount; s++)
        {
            Coordinates.SetSlice(cube, s);
            Assert.Equal(s, Coordinates.GetSlice(cube));
            var slots = string.Join(",", cube.Ep.Select((e, i) => e >= 8 ? i : -1).Where(i => i >= 0));
            Assert.True(seen.Add(slots));
        }
    }

    [Fact]
    public void PermutationCoordinatesRoundTrip()
    {
        var cube = CubieCube.Solved;
        for (var p = 0; p < Coordinates.CornerPermCount; p += 7)
        {
            Coordinates.SetCornerPerm(cube, p);
            Assert.Equal(p, Coordinates.GetCornerPerm(cube));
        }
        for (var p = 0; p < Coordinates.UdEdgePermCount; p += 11)
        {
            Coordinates.SetUdEdgePerm(cube, p);
            Assert.Equal(p, Coordinates.GetUdEdgePerm(cube));
        }
        for (var p = 0; p < Coordinates.SlicePermCount; p++)
        {
            Coordinates.SetSlicePerm(cube, p);
            Assert.Equal(p, Coordinates.GetSlicePerm(cube));
        }
    }

    [Fact]
    public void CoordinatesOfRandomCubesAreInRange()
    {
        var rng = new Random(3);
        for (var i = 0; i < 500; i++)
        {
            var cube = CubieCubeTests.RandomValidCube(rng);
            Assert.InRange(Coordinates.GetTwist(cube), 0, Coordinates.TwistCount - 1);
            Assert.InRange(Coordinates.GetFlip(cube), 0, Coordinates.FlipCount - 1);
            Assert.InRange(Coordinates.GetSlice(cube), 0, Coordinates.SliceCount - 1);
            Assert.InRange(Coordinates.GetCornerPerm(cube), 0, Coordinates.CornerPermCount - 1);
        }
    }
}
