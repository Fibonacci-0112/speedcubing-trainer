using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Tests.Algorithms;

public class LastLayerArrowsTests
{
    private static readonly int[] Corners = [0, 2, 6, 8];
    private static readonly int[] Edges = [1, 3, 5, 7];

    [Fact]
    public void SolvedCubeHasNoArrows()
    {
        Assert.Empty(LastLayerArrows.From(CubeState.Solved));
    }

    [Fact]
    public void AnAufAloneHasNoArrows()
    {
        Assert.Empty(LastLayerArrows.From(CubeState.Solved.Apply("U")));
        Assert.Empty(LastLayerArrows.From(CubeState.Solved.Apply("U2")));
    }

    [Fact]
    public void BrokenF2LHasNoArrows()
    {
        Assert.Empty(LastLayerArrows.From(CubeState.Solved.Apply("R")));
    }

    [Theory]
    [InlineData("PLL-Ua", 0, 3)]
    [InlineData("PLL-Ub", 0, 3)]
    [InlineData("PLL-H", 2, 0)]
    [InlineData("PLL-Z", 2, 0)]
    [InlineData("PLL-Aa", 0, 3)]
    [InlineData("PLL-Ab", 0, 3)]
    [InlineData("PLL-E", 2, 0)]
    [InlineData("PLL-T", 2, 0)]
    [InlineData("PLL-F", 2, 0)]
    [InlineData("PLL-Ja", 2, 0)]
    [InlineData("PLL-Jb", 2, 0)]
    [InlineData("PLL-Ra", 2, 0)]
    [InlineData("PLL-Rb", 2, 0)]
    [InlineData("PLL-V", 2, 0)]
    [InlineData("PLL-Y", 2, 0)]
    [InlineData("PLL-Na", 2, 0)]
    [InlineData("PLL-Nb", 2, 0)]
    [InlineData("PLL-Ga", 0, 6)]
    [InlineData("PLL-Gb", 0, 6)]
    [InlineData("PLL-Gc", 0, 6)]
    [InlineData("PLL-Gd", 0, 6)]
    public void CasesGetTheirSwapsAndCycles(string id, int twoWay, int oneWay)
    {
        var c = AlgorithmSetLoader.Load(AlgorithmSetKind.PLL).Cases.Single(x => x.Id == id);
        var arrows = LastLayerArrows.From(c.State);
        Assert.Equal(twoWay, arrows.Count(a => a.TwoWay));
        Assert.Equal(oneWay, arrows.Count(a => !a.TwoWay));
    }

    [Fact]
    public void EveryPllCaseHasArrowsBetweenLikePieces()
    {
        foreach (var c in AlgorithmSetLoader.Load(AlgorithmSetKind.PLL).Cases)
        {
            var arrows = LastLayerArrows.From(c.State);
            Assert.NotEmpty(arrows);
            Assert.All(arrows, a => Assert.True(
                Corners.Contains(a.From) == Corners.Contains(a.To) && a.From != a.To,
                $"{c.Id}: {a}"));
        }
    }

    [Fact]
    public void ArrowsPointFromThePieceToWhereItBelongs()
    {
        // A T perm on a solved cube swaps two edges and two corners.
        var state = CubeState.Solved.Apply("R U R' U' R' F R2 U' R' U' R U R' F'");
        var arrows = LastLayerArrows.From(state);
        Assert.Contains(arrows, a => a.TwoWay && Edges.Contains(a.From) && Edges.Contains(a.To));
        Assert.Contains(arrows, a => a.TwoWay && Corners.Contains(a.From) && Corners.Contains(a.To));
    }
}
