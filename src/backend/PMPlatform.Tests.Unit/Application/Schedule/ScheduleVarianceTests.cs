using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Tests.Unit.Application.Schedule;

/// <summary>
/// The acceptance criterion's second half: variance is the Current Forecast against the Approved Baseline — the baseline's own
/// copy of the dates — never the forecast against itself and never against the working plan; positive is late (BR-SCH-032).
/// </summary>
public sealed class ScheduleVarianceTests
{
    private static readonly DateOnly Day1 = new(2026, 11, 1);

    [Fact]
    public void VarianceIsTheForecastAgainstTheBaselineCopy()
    {
        ScheduleActivity activity = ScheduleCalculationTests.Leaf("1", Day1, 10);
        activity.ForecastStartDate = Day1.AddDays(2);
        activity.ForecastFinishDate = Day1.AddDays(14);

        ActivityVariance variance = ScheduleVariance.Of(activity, Frozen(activity, Day1, Day1.AddDays(9)));

        Assert.Equal((2, 5), (variance.StartVarianceDays, variance.FinishVarianceDays));
    }

    [Fact]
    public void EarlyIsNegative()
    {
        ScheduleActivity activity = ScheduleCalculationTests.Leaf("1", Day1, 10);
        activity.ForecastStartDate = Day1.AddDays(-3);
        activity.ForecastFinishDate = Day1.AddDays(6);

        Assert.Equal((-3, -3), Pair(ScheduleVariance.Of(activity, Frozen(activity, Day1, Day1.AddDays(9)))));
    }

    /// <summary>
    /// The working plan moves on after approval (a rebaseline in preparation) and the forecast with it; the variance is still
    /// measured from the frozen copy, so it shows the slip instead of hiding it.
    /// </summary>
    [Fact]
    public void AWorkingPlanThatMovesAfterApprovalIsNoReference()
    {
        ScheduleActivity activity = ScheduleCalculationTests.Leaf("1", Day1, 10);
        BaselineActivity frozen = Frozen(activity, Day1, Day1.AddDays(9));
        activity.PlannedStartDate = Day1.AddDays(30);
        activity.PlannedFinishDate = Day1.AddDays(39);
        activity.ForecastStartDate = Day1.AddDays(30);
        activity.ForecastFinishDate = Day1.AddDays(39);

        Assert.Equal((30, 30), Pair(ScheduleVariance.Of(activity, frozen)));
    }

    [Fact]
    public void WithoutABaselineEntryThereIsNoVarianceRatherThanZero()
    {
        ScheduleActivity activity = ScheduleCalculationTests.Leaf("1", Day1, 10);

        Assert.Equal((null, null), Pair(ScheduleVariance.Of(activity, null)));
    }

    [Fact]
    public void TheProjectFinishVarianceIsTheForecastFinishAgainstTheBaselineFinish()
    {
        ProjectBaseline baseline = new() { BaselineFinishDate = Day1.AddDays(100) };

        Assert.Equal(7, ScheduleVariance.ProjectFinish(baseline, Day1.AddDays(107)));
        Assert.Equal(-1, ScheduleVariance.ProjectFinish(baseline, Day1.AddDays(99)));
        Assert.Null(ScheduleVariance.ProjectFinish(null, Day1));
        Assert.Null(ScheduleVariance.ProjectFinish(baseline, null));
    }

    private static BaselineActivity Frozen(ScheduleActivity activity, DateOnly start, DateOnly finish) => new()
    {
        ScheduleActivityId = activity.Id,
        PlannedStartDate = start,
        PlannedFinishDate = finish,
        PlannedDurationDays = WorkingDays.Span(start, finish),
    };

    private static (int?, int?) Pair(ActivityVariance variance) => (variance.StartVarianceDays, variance.FinishVarianceDays);
}
