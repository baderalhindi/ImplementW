using PMPlatform.Application.Features.Progress;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;

namespace PMPlatform.Tests.Unit.Application.Progress;

/// <summary>Reporting periods: contiguous, cadence-long, from activation or from the day after the ADR-014 intake date.</summary>
public sealed class ReportingCalendarTests
{
    private static readonly DateOnly Day = new(2026, 3, 1);

    [Fact]
    public void ARegisteredProjectReportsFromItsActivationDate() =>
        Assert.Equal(Day, ReportingCalendar.FirstPeriodStart(Project(activatedAt: new DateTimeOffset(2026, 3, 1, 21, 0, 0, TimeSpan.Zero), intake: null)));

    /// <summary>The intake date is the opening position's own one-day period, so regular reporting starts the day after.</summary>
    [Fact]
    public void ALegacyProjectReportsFromTheDayAfterItsIntake() =>
        Assert.Equal(Day.AddDays(1), ReportingCalendar.FirstPeriodStart(Project(activatedAt: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), intake: Day)));

    [Fact]
    public void AProjectNeitherActivatedNorTakenInHasNoPeriods() =>
        Assert.Null(ReportingCalendar.FirstPeriodStart(Project(activatedAt: null, intake: null)));

    [Fact]
    public void PeriodsFollowEachOtherWithoutGapOrOverlapUntilToday()
    {
        List<(DateOnly Start, DateOnly End)> periods = [.. ReportingCalendar.PeriodsBegunBy(Day, 7, Day.AddDays(14))];

        Assert.Equal([(Day, Day.AddDays(6)), (Day.AddDays(7), Day.AddDays(13)), (Day.AddDays(14), Day.AddDays(20))], periods);
    }

    [Fact]
    public void APeriodNotYetBegunIsNotGenerated() =>
        Assert.Empty(ReportingCalendar.PeriodsBegunBy(Day, 30, Day.AddDays(-1)));

    [Fact]
    public void TheNextPeriodStartsTheDayAfterTheLastOne()
    {
        Assert.Equal(Day, ReportingCalendar.NextPeriodStart(Day, null));
        Assert.Equal(Day.AddDays(30), ReportingCalendar.NextPeriodStart(Day, Day.AddDays(29)));
        Assert.Equal(Day, ReportingCalendar.NextPeriodStart(Day, Day.AddDays(-1)));  // the opening position's period, the intake date
    }

    private static ProjectFacts Project(DateTimeOffset? activatedAt, DateOnly? intake) =>
        new(Guid.NewGuid(), "PRJ-000001", Guid.NewGuid(), null, Guid.NewGuid(), ProjectLifecycleState.Active, Guid.NewGuid(), intake, activatedAt, ParticipationMode.AhdaManaged);
}
