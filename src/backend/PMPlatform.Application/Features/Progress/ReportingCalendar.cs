using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Reporting periods follow one another without gap or overlap, each the governance profile's cadence long (ADR-015). The
/// first starts on the activation date, or the day after the intake date for a project that entered by the legacy intake
/// path, whose opening position occupies the intake date itself (ADR-014). A period is due at its end. Dates are UTC
/// calendar dates (progress-update.md F-7).
/// </summary>
internal static class ReportingCalendar
{
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    /// <summary>Where the first regular period starts; null for a project that has neither been activated nor taken in.</summary>
    public static DateOnly? FirstPeriodStart(ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return project.LegacyIntakeDate?.AddDays(1) ?? (project.ActivatedAt is { } activatedAt ? Today(activatedAt) : null);
    }

    /// <summary>The start of the period after <paramref name="lastPeriodEnd"/>, or the first start when there is none.</summary>
    public static DateOnly NextPeriodStart(DateOnly firstPeriodStart, DateOnly? lastPeriodEnd) =>
        lastPeriodEnd is { } end && end >= firstPeriodStart ? end.AddDays(1) : firstPeriodStart;

    /// <summary>The periods from <paramref name="start"/> that have begun by <paramref name="today"/>, as (start, end).</summary>
    public static IEnumerable<(DateOnly Start, DateOnly End)> PeriodsBegunBy(DateOnly start, int cadenceDays, DateOnly today)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cadenceDays);
        for (DateOnly periodStart = start; periodStart <= today; periodStart = periodStart.AddDays(cadenceDays))
        {
            yield return (periodStart, periodStart.AddDays(cadenceDays - 1));
        }
    }
}
