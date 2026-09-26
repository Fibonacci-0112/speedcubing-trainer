using SpeedcubingTrainer.Core.Statistics;

namespace SpeedcubingTrainer.Core.Tests.Statistics;

public class WcaStatisticsTests
{
    private static SolveTime T(double seconds, Penalty penalty = Penalty.None) => new((int)Math.Round(seconds * 1000), penalty);

    [Fact]
    public void Ao5TrimsBestAndWorst()
    {
        SolveTime[] window = [T(10), T(30), T(12), T(11), T(13)];
        Assert.Equal(12000, WcaStatistics.AverageOf(window));
    }

    [Fact]
    public void Ao5WithOneDnfCountsItAsWorst()
    {
        SolveTime[] window = [T(10), T(0, Penalty.Dnf), T(12), T(11), T(13)];
        Assert.Equal(12000, WcaStatistics.AverageOf(window));
    }

    [Fact]
    public void Ao5WithTwoDnfsIsDnf()
    {
        SolveTime[] window = [T(10), T(0, Penalty.Dnf), T(12), T(11, Penalty.Dnf), T(13)];
        Assert.Null(WcaStatistics.AverageOf(window));
    }

    [Fact]
    public void Plus2AddsTwoSeconds()
    {
        Assert.Equal(12000, new SolveTime(10000, Penalty.Plus2).EffectiveMs);
        SolveTime[] window = [T(10), T(10, Penalty.Plus2), T(12), T(11), T(13)];
        Assert.Equal(11667, WcaStatistics.AverageOf(window)); // 11, 12, 12 middle
    }

    [Fact]
    public void Mo3WithDnfIsDnf()
    {
        Assert.Equal(11000, WcaStatistics.MeanOf([T(10), T(11), T(12)]));
        Assert.Null(WcaStatistics.MeanOf([T(10), T(11, Penalty.Dnf), T(12)]));
    }

    [Theory]
    [InlineData(5, 1)]
    [InlineData(12, 1)]
    [InlineData(50, 3)]
    [InlineData(100, 5)]
    public void TrimCountsFollowWcaRules(int size, int trim)
    {
        Assert.Equal(trim, WcaStatistics.TrimCount(size));
    }

    [Fact]
    public void Ao50AllowsThreeDnfs()
    {
        var window = Enumerable.Range(0, 50).Select(i => T(10 + i * 0.1)).ToArray();
        window[3] = T(0, Penalty.Dnf);
        window[7] = T(0, Penalty.Dnf);
        window[9] = T(0, Penalty.Dnf);
        Assert.NotNull(WcaStatistics.AverageOf(window));
        window[11] = T(0, Penalty.Dnf);
        Assert.Null(WcaStatistics.AverageOf(window));
    }

    [Fact]
    public void RollingStatsNeedFullWindow()
    {
        var solves = new List<SolveTime> { T(10), T(11), T(12), T(13) };
        Assert.Null(WcaStatistics.Rolling(solves, StatKind.Ao5, 3, out var has));
        Assert.False(has);
        Assert.Equal(12000, WcaStatistics.Rolling(solves, StatKind.Mo3, 3, out has));
        Assert.True(has);
        Assert.Equal(13000, WcaStatistics.Rolling(solves, StatKind.Single, 3, out _));
    }

    [Fact]
    public void SummaryTracksBestWindows()
    {
        var solves = new List<SolveTime> { T(15), T(14), T(13), T(12), T(11), T(10), T(20), T(9, Penalty.Dnf) };
        var summary = SessionStatistics.Summarize(solves);
        Assert.Equal(8, summary.Count);
        Assert.Equal(1, summary.DnfCount);
        Assert.Equal(10000, summary.BestSingle);
        Assert.Equal(20000, summary.WorstSingle);
        Assert.Equal(13571, summary.Mean); // (15+14+13+12+11+10+20)/7 = 13.571
        var ao5 = summary.Stats.Single(s => s.Kind == StatKind.Ao5);
        Assert.Equal(12000, ao5.Best!.Ms); // 14 13 12 11 10 -> 11 12 13 (first of two equal windows)
        Assert.Equal(5, ao5.Best.EndIndex);
        Assert.Equal(14333, ao5.Current); // 12 11 10 20 DNF -> DNF is worst, trims DNF and 10 -> 11 12 20
        var single = summary.Stats.Single(s => s.Kind == StatKind.Single);
        Assert.Null(single.Current);
        Assert.True(single.HasCurrent);
    }

    [Fact]
    public void Ao5CurrentWithTrailingDnf()
    {
        var solves = new List<SolveTime> { T(12), T(11), T(10), T(20), T(9, Penalty.Dnf) };
        Assert.Equal(14333, WcaStatistics.Rolling(solves, StatKind.Ao5, 4, out _));
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(9999, "9.99")]
    [InlineData(12345, "12.34")]
    [InlineData(60000, "1:00.00")]
    [InlineData(83456, "1:23.45")]
    [InlineData(3600000, "60:00.00")]
    public void FormatsTimes(int ms, string expected)
    {
        Assert.Equal(expected, TimeFormat.Format(ms));
    }

    [Fact]
    public void FormatsPenalties()
    {
        Assert.Equal("DNF", TimeFormat.Format(new SolveTime(1000, Penalty.Dnf)));
        Assert.Equal("12.00+", TimeFormat.Format(new SolveTime(10000, Penalty.Plus2)));
    }
}
