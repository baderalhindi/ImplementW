using PMPlatform.Application.Features.Progress;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>ADR-009 option C: actual rolled up through the work breakdown weighted by planned duration; planned from the baseline.</summary>
public sealed class ProgressRollupTests
{
    private static readonly Guid Phase = Guid.NewGuid();
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Fact]
    public void ActualIsWeightedByPlannedDurationNotAveraged()
    {
        // 10 days at 100 % and 30 days at 0 %: 25 %, where an unweighted average (option B) would say 50 %.
        Assert.Equal(25m, ProgressRollup.Actual([Leaf(10, 100), Leaf(30, 0)]));
    }

    [Fact]
    public void ActualRollsUpThroughSummariesAndIgnoresASummarysOwnFigure()
    {
        // The phase's own 90 % is derived elsewhere and not read; its children (20 days at 50 %, 20 at 100 %) and a sibling
        // task (40 days at 0 %) give (1000 + 2000 + 0) / 80.
        Assert.Equal(
            37.5m,
            ProgressRollup.Actual(
            [
                new WorkItemProgress(Phase, null, 40, 90),
                new WorkItemProgress(Guid.NewGuid(), Phase, 20, 50),
                new WorkItemProgress(Guid.NewGuid(), Phase, 20, 100),
                Leaf(40, 0),
            ]));
    }

    [Fact]
    public void ActualIsRoundedToTheFourPlacesTheColumnHolds() =>
        Assert.Equal(33.3333m, ProgressRollup.Actual([Leaf(1, 100), Leaf(2, 0)]));

    [Fact]
    public void ActualHasNoBasisWithoutAPlannedDuration()
    {
        Assert.Null(ProgressRollup.Actual([]));
        Assert.Null(ProgressRollup.Actual([Leaf(0, 100), Leaf(0, 0)]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void AnActualOutsideZeroToHundredIsRefused(decimal percent) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRollup.Actual([Leaf(5, percent)]));

    [Fact]
    public void ANegativeDurationIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRollup.Actual([Leaf(-1, 50)]));

    [Theory]
    [InlineData(-1, 0)]       // the day before the start
    [InlineData(0, 10)]       // the first of ten days, both ends counted
    [InlineData(4, 50)]
    [InlineData(9, 100)]      // the finish day
    [InlineData(400, 100)]
    public void PlannedAccruesLinearlyOverAnActivitysDays(int daysFromStart, decimal expected) =>
        Assert.Equal(expected, ProgressRollup.Planned(Plan(Activity(0, 10)), Start.AddDays(daysFromStart)));

    [Fact]
    public void PlannedIsWeightedByDuration()
    {
        // Day 10: the 10-day activity is done, the 30-day one starting with it is a third done: (10 + 10) / 40.
        Assert.Equal(50m, ProgressRollup.Planned(Plan(Activity(0, 10), Activity(0, 30)), Start.AddDays(9)));
    }

    [Fact]
    public void PlannedIsUnknownWithoutABaselineOrWithADeclaredOneThatHasNoPlan()
    {
        Assert.Null(ProgressRollup.Planned(null, Start));
        Assert.Null(ProgressRollup.Planned(Plan(), Start));
    }

    [Fact]
    public void AnActivityFinishingBeforeItStartsIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProgressRollup.Planned(
            new BaselinePlan(Guid.NewGuid(), [new BaselineActivity(Guid.NewGuid(), Start, Start.AddDays(-1))]), Start));

    private static WorkItemProgress Leaf(decimal days, decimal percent) => new(Guid.NewGuid(), null, days, percent);

    private static BaselineActivity Activity(int startOffset, int days) => new(Guid.NewGuid(), Start.AddDays(startOffset), Start.AddDays(startOffset + days - 1));

    private static BaselinePlan Plan(params BaselineActivity[] activities) => new(Guid.NewGuid(), activities);
}
