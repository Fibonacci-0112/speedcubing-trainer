using SpeedcubingTrainer.Core.Algorithms;
using SpeedcubingTrainer.Core.Persistence;
using SpeedcubingTrainer.Core.Training;

namespace SpeedcubingTrainer.Core.Tests.Training;

public class Sm2SchedulerTests
{
    private static readonly DateOnly Today = new(2026, 9, 26);

    [Fact]
    public void GoodThreeTimesGivesStandardIntervals()
    {
        var scheduler = new Sm2Scheduler();
        var state = new ReviewState();
        Assert.True(state.IsNew);
        state = scheduler.Review(state, Grade.Good, Today);
        Assert.Equal(1, state.IntervalDays);
        Assert.Equal(Today.AddDays(1), state.Due);
        state = scheduler.Review(state, Grade.Good, Today.AddDays(1));
        Assert.Equal(6, state.IntervalDays);
        state = scheduler.Review(state, Grade.Good, Today.AddDays(7));
        Assert.Equal(15, state.IntervalDays);
        Assert.Equal(2.5, state.Ease);
        Assert.Equal(3, state.Repetitions);
    }

    [Fact]
    public void AgainResetsAndLowersEase()
    {
        var scheduler = new Sm2Scheduler();
        var state = scheduler.Review(new ReviewState(), Grade.Good, Today);
        state = scheduler.Review(state, Grade.Again, Today.AddDays(1));
        Assert.Equal(0, state.IntervalDays);
        Assert.Equal(0, state.Repetitions);
        Assert.Equal(1, state.Lapses);
        Assert.Equal(2.3, state.Ease);
        Assert.True(state.IsDue(Today.AddDays(1)));
        Assert.False(state.IsNew);
    }

    [Fact]
    public void EaseNeverDropsBelowMinimum()
    {
        var scheduler = new Sm2Scheduler();
        var state = new ReviewState();
        for (var i = 0; i < 20; i++)
        {
            state = scheduler.Review(state, Grade.Again, Today);
        }
        Assert.Equal(ReviewState.MinimumEase, state.Ease);
    }

    [Fact]
    public void EasyIsLongerThanGoodAndHardIsShorter()
    {
        var scheduler = new Sm2Scheduler();
        var state = scheduler.Review(new ReviewState(), Grade.Good, Today);
        state = scheduler.Review(state, Grade.Good, Today.AddDays(1));
        var preview = scheduler.PreviewIntervals(state);
        Assert.True(preview[Grade.Easy] > preview[Grade.Good]);
        Assert.True(preview[Grade.Good] > preview[Grade.Hard]);
        Assert.Equal(0, preview[Grade.Again]);
        foreach (var grade in Enum.GetValues<Grade>())
        {
            Assert.Equal(preview[grade], scheduler.Review(state, grade, Today).IntervalDays);
        }
    }

    [Fact]
    public void IntervalsAreCapped()
    {
        var scheduler = new Sm2Scheduler(maxIntervalDays: 30);
        var state = new ReviewState();
        for (var i = 0; i < 10; i++)
        {
            state = scheduler.Review(state, Grade.Easy, Today);
        }
        Assert.Equal(30, state.IntervalDays);
    }

    [Fact]
    public void ReviewStateRoundTripsThroughProgressRecord()
    {
        var state = new ReviewState(2.1, 12, 4, 1, Today);
        var record = new CaseProgressRecord();
        state.ApplyTo(record);
        Assert.Equal(state, ReviewState.From(record));
    }

    [Fact]
    public void QueueListsDueBeforeNewAndLimitsNew()
    {
        var set = AlgorithmSetLoader.Load(AlgorithmSetKind.PLL);
        var progress = new Dictionary<string, CaseProgressRecord>();
        var due = new CaseProgressRecord { Status = LearningStatus.Learning };
        new ReviewState(2.5, 3, 2, 0, Today.AddDays(-1)).ApplyTo(due);
        progress["PLL-T"] = due;
        var notDue = new CaseProgressRecord { Status = LearningStatus.Learning };
        new ReviewState(2.5, 3, 2, 0, Today.AddDays(5)).ApplyTo(notDue);
        progress["PLL-Y"] = notDue;

        var queue = ReviewQueue.Build(set.Cases, progress, Today, newPerDay: 3);
        Assert.Equal("PLL-T", queue[0].Case.Id);
        Assert.False(queue[0].IsNew);
        Assert.Equal(4, queue.Count);
        Assert.All(queue.Skip(1), e => Assert.True(e.IsNew));
        Assert.DoesNotContain(queue, e => e.Case.Id == "PLL-Y");
        Assert.Equal(1, ReviewQueue.CountDue(set.Cases, progress, Today));

        var learningOnly = ReviewQueue.Build(set.Cases, progress, Today, 5, (_, p) => p.Status == LearningStatus.Learning);
        Assert.Single(learningOnly);
    }

    [Fact]
    public void DrillStatsTrackBestsAndRecent()
    {
        var record = new CaseProgressRecord();
        DrillStats.Record(record, new DrillAttemptRecord(DateTimeOffset.UtcNow, 900, 1500, true));
        DrillStats.Record(record, new DrillAttemptRecord(DateTimeOffset.UtcNow, 700, 1700, true));
        DrillStats.Record(record, new DrillAttemptRecord(DateTimeOffset.UtcNow, 100, 100, false));
        Assert.Equal(3, record.DrillAttempts);
        Assert.Equal(2, record.DrillSuccesses);
        Assert.Equal(700, record.BestRecognitionMs);
        Assert.Equal(1500, record.BestExecutionMs);
        Assert.Equal(800, DrillStats.MeanRecognitionMs(record));
        Assert.Equal(2400, DrillStats.MeanTotalMs(record));
        for (var i = 0; i < 30; i++)
        {
            DrillStats.Record(record, new DrillAttemptRecord(DateTimeOffset.UtcNow, 500, 500, true));
        }
        Assert.Equal(DrillStats.RecentLimit, record.RecentDrills.Count);
    }
}
