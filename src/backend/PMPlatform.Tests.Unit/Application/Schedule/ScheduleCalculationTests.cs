using PMPlatform.Application.Features.Schedule;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Tests.Unit.Application.Schedule;

/// <summary>
/// The spec's §12.3 date rules: a leaf from its requested start and duration (day 1 is the start), moved by FS, SS and FF
/// constraints with their lag; summaries span their children; cancelled nodes take no part; the forecast follows the plan only
/// where told. Every day is a working day until a calendar exists (schedule-baseline.md F-3).
/// </summary>
public sealed class ScheduleCalculationTests
{
    private static readonly DateOnly Day1 = new(2026, 11, 1);

    [Fact]
    public void ALeafFinishesOnTheLastDayOfItsDuration()
    {
        ScheduleActivity leaf = Leaf("1", Day1, 10);

        ScheduleCalculation.Recalculate([leaf], [], _ => true);

        Assert.Equal((Day1, Day1.AddDays(9)), (leaf.PlannedStartDate, leaf.PlannedFinishDate));
    }

    [Theory]
    [InlineData(ScheduleDependencyType.Fs, 0, 10)]   // the day after the predecessor's finish (day 10)
    [InlineData(ScheduleDependencyType.Fs, 2, 12)]
    [InlineData(ScheduleDependencyType.Ss, 0, 0)]    // with the predecessor's start
    [InlineData(ScheduleDependencyType.Ss, 3, 3)]
    [InlineData(ScheduleDependencyType.Ff, 0, 5)]    // a 5-day successor finishing with the predecessor on day 10
    [InlineData(ScheduleDependencyType.Ff, 1, 6)]
    public void ADependencyHoldsTheSuccessorBack(ScheduleDependencyType type, int lag, int startOffset)
    {
        ScheduleActivity predecessor = Leaf("1", Day1, 10);
        ScheduleActivity successor = Leaf("2", Day1, 5);

        ScheduleCalculation.Recalculate([predecessor, successor], [Link(predecessor, successor, type, lag)], _ => true);

        Assert.Equal(Day1.AddDays(startOffset), successor.PlannedStartDate);
        Assert.Equal(WorkingDays.FinishOf(successor.PlannedStartDate, 5), successor.PlannedFinishDate);
    }

    [Fact]
    public void ARequestedStartLaterThanEveryConstraintStands()
    {
        ScheduleActivity predecessor = Leaf("1", Day1, 3);
        ScheduleActivity successor = Leaf("2", Day1.AddDays(20), 2);

        ScheduleCalculation.Recalculate([predecessor, successor], [Link(predecessor, successor, ScheduleDependencyType.Fs)], _ => true);

        Assert.Equal(Day1.AddDays(20), successor.PlannedStartDate);
    }

    [Fact]
    public void AChainIsPushedThroughInOrderWhateverTheListOrder()
    {
        ScheduleActivity a = Leaf("1", Day1, 2);
        ScheduleActivity b = Leaf("2", Day1, 2);
        ScheduleActivity c = Leaf("3", Day1, 2);

        ScheduleCalculation.Recalculate([c, b, a], [Link(b, c, ScheduleDependencyType.Fs), Link(a, b, ScheduleDependencyType.Fs)], _ => true);

        Assert.Equal([Day1, Day1.AddDays(2), Day1.AddDays(4)], [a.PlannedStartDate, b.PlannedStartDate, c.PlannedStartDate]);
    }

    [Fact]
    public void ASummarySpansItsChildrenAndIsDerivedFromHavingThem()
    {
        ScheduleActivity phase = Leaf("1", Day1.AddDays(50), 1);
        ScheduleActivity first = Leaf("1.1", Day1.AddDays(3), 4, phase);
        ScheduleActivity second = Leaf("1.2", Day1.AddDays(10), 5, phase);

        ScheduleCalculation.Recalculate([phase, first, second], [], _ => true);

        Assert.Equal(ScheduleActivityKind.Summary, phase.ActivityKind);
        Assert.Equal((Day1.AddDays(3), Day1.AddDays(14), 12), (phase.PlannedStartDate, phase.PlannedFinishDate, phase.PlannedDurationDays));
        Assert.Equal((phase.PlannedStartDate, phase.PlannedFinishDate), (phase.ForecastStartDate, phase.ForecastFinishDate));
    }

    [Fact]
    public void ACancelledActivityTakesNoPart()
    {
        ScheduleActivity phase = Leaf("1", Day1, 1);
        ScheduleActivity live = Leaf("1.1", Day1, 4, phase);
        ScheduleActivity cancelled = Leaf("1.2", Day1.AddDays(40), 5, phase);
        cancelled.Status = ScheduleActivityStatus.Cancelled;

        ScheduleCalculation.Recalculate([phase, live, cancelled], [Link(cancelled, live, ScheduleDependencyType.Fs)], _ => true);

        Assert.Equal((Day1, Day1.AddDays(3)), (phase.PlannedStartDate, phase.PlannedFinishDate));
        Assert.Equal(Day1, live.PlannedStartDate);
        Assert.Equal(Day1.AddDays(3), ScheduleCalculation.ForecastFinish([phase, live, cancelled]));
    }

    /// <summary>BR-SCH-004, BR-SCH-031: under baseline control a plan change moves no forecast; only summaries' forecasts roll up.</summary>
    [Fact]
    public void TheForecastFollowsThePlanOnlyWhereTold()
    {
        ScheduleActivity phase = Leaf("1", Day1, 1);
        ScheduleActivity leaf = Leaf("1.1", Day1, 4, phase);
        ScheduleCalculation.Recalculate([phase, leaf], [], _ => true);

        leaf.RequestedStartDate = Day1.AddDays(10);
        leaf.ForecastStartDate = Day1.AddDays(2);
        leaf.ForecastFinishDate = Day1.AddDays(8);
        ScheduleCalculation.Recalculate([phase, leaf], [], _ => false);

        Assert.Equal(Day1.AddDays(10), leaf.PlannedStartDate);
        Assert.Equal((Day1.AddDays(2), Day1.AddDays(8)), (leaf.ForecastStartDate, leaf.ForecastFinishDate));
        Assert.Equal((Day1.AddDays(2), Day1.AddDays(8)), (phase.ForecastStartDate, phase.ForecastFinishDate));
    }

    [Fact]
    public void OnlyWhatMovedIsReported()
    {
        ScheduleActivity a = Leaf("1", Day1, 2);
        ScheduleActivity b = Leaf("2", Day1, 2);
        ScheduleCalculation.Recalculate([a, b], [], _ => true);

        Assert.Empty(ScheduleCalculation.Recalculate([a, b], [], _ => true));
        Assert.Equal([b], ScheduleCalculation.Recalculate([a, b], [Link(a, b, ScheduleDependencyType.Fs)], _ => true));
    }

    [Fact]
    public void ThePlannedFinishIsTheLatestLiveLeafsAndNoneWithoutOne()
    {
        ScheduleActivity a = Leaf("1", Day1, 2);
        ScheduleActivity b = Leaf("2", Day1, 9);
        ScheduleCalculation.Recalculate([a, b], [], _ => true);

        Assert.Equal(Day1.AddDays(8), ScheduleCalculation.PlannedFinish([a, b]));
        Assert.Null(ScheduleCalculation.PlannedFinish([]));
    }

    internal static ScheduleActivity Leaf(string wbs, DateOnly requestedStart, int duration, ScheduleActivity? parent = null) => new()
    {
        Id = Guid.NewGuid(),
        ParentActivityId = parent?.Id,
        WbsCode = wbs,
        Name = new NarrativeText($"Activity {wbs}", Language.En),
        ActivityKind = ScheduleActivityKind.Activity,
        RequestedStartDate = requestedStart,
        PlannedStartDate = requestedStart,
        PlannedFinishDate = requestedStart,
        PlannedDurationDays = duration,
        ForecastStartDate = requestedStart,
        ForecastFinishDate = requestedStart,
        Status = ScheduleActivityStatus.Planned,
    };

    private static ScheduleDependency Link(ScheduleActivity predecessor, ScheduleActivity successor, ScheduleDependencyType type, int lag = 0) =>
        new() { Id = Guid.NewGuid(), PredecessorActivityId = predecessor.Id, SuccessorActivityId = successor.Id, DependencyType = type, LagDays = lag };
}
