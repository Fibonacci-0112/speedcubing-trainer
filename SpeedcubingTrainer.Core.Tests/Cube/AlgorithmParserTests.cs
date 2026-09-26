using SpeedcubingTrainer.Core.Cube;

namespace SpeedcubingTrainer.Core.Tests.Cube;

public class AlgorithmParserTests
{
    [Theory]
    [InlineData("R U R' U'", "R U R' U'")]
    [InlineData("(R U R' U')3", "R U R' U' R U R' U' R U R' U'")]
    [InlineData("(R U R' U')*2 F", "R U R' U' R U R' U' F")]
    [InlineData("F (R U R' U')x2 F'", "F R U R' U' R U R' U' F'")]
    [InlineData("[R U R' U']", "R U R' U'")]
    [InlineData("((R U) R')2", "R U R' R U R'")]
    [InlineData("r U R' U'", "Rw U R' U'")]
    [InlineData("Rw U R' U'", "Rw U R' U'")]
    [InlineData("U2'", "U2")]
    [InlineData("U'2", "U2")]
    [InlineData("U3", "U'")]
    [InlineData("x y' z2 M E' S2", "x y' z2 M E' S2")]
    [InlineData("R  U\tR'\n U'", "R U R' U'")]
    [InlineData("R U // sexy move start\nR' U'", "R U R' U'")]
    [InlineData("R U, R' U'", "R U R' U'")]
    [InlineData("", "")]
    [InlineData("   // only a comment", "")]
    public void ParsesValidInput(string input, string expected)
    {
        var alg = Algorithm.Parse(input);
        Assert.Equal(expected, alg.ToString());
    }

    [Theory]
    [InlineData("2R", 0)]
    [InlineData("R U (R' U'", 4)]
    [InlineData("R U R' U')", 9)]
    [InlineData("R Q", 2)]
    [InlineData("Rx", 0)]
    [InlineData("Mw", 1)]
    [InlineData("R22", 0)]
    public void RejectsInvalidInputWithPosition(string input, int position)
    {
        var ex = Assert.Throws<AlgorithmParseException>(() => Algorithm.Parse(input));
        Assert.Equal(position, ex.Position);
        Assert.False(Algorithm.TryParse(input, out _));
    }

    [Fact]
    public void LowercaseStyleWritesWideMovesAsLowercase()
    {
        var alg = Algorithm.Parse("Rw U Lw' Uw2");
        Assert.Equal("r U l' u2", alg.ToString(NotationStyle.Lowercase));
        Assert.Equal("Rw U Lw' Uw2", alg.ToString(NotationStyle.Wide));
    }

    [Fact]
    public void HtmLengthIgnoresRotations()
    {
        Assert.Equal(4, Algorithm.Parse("y R U R' U' x2").HtmLength);
        Assert.Equal(6, Algorithm.Parse("y R U R' U' x2").Count);
    }

    [Fact]
    public void SimplifyMergesAndCancels()
    {
        Assert.Equal("R2 U", Algorithm.Parse("R R U").Simplify().ToString());
        Assert.Equal("", Algorithm.Parse("R U U' R'").Simplify().ToString());
        Assert.Equal("U'", Algorithm.Parse("U2 U").Simplify().ToString());
        Assert.Equal("R U R' U'", Algorithm.Parse("R U R' U'").Simplify().ToString());
    }

    [Fact]
    public void EqualityIsStructural()
    {
        Assert.Equal(Algorithm.Parse("R U"), Algorithm.Parse("R  U"));
        Assert.NotEqual(Algorithm.Parse("R U"), Algorithm.Parse("R U'"));
        Assert.Equal(Algorithm.Parse("R U").GetHashCode(), Algorithm.Parse("R U").GetHashCode());
    }
}
