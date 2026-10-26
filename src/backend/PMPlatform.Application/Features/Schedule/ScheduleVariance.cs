using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// Schedule variance (the spec's §9.16, DCL-SCH-15): the Current Forecast measured against the ACTIVE baseline, in signed
/// working days, positive = late. The reference is always the baseline's own copy of the dates — never the working
/// schedule's planned dates, which a planner keeps editing, and never the forecast itself. Without a baseline entry there is
/// no variance, not a zero one.
/// </summary>
internal static class ScheduleVariance
{
    /// <summary>An activity's start and finish variance against its entry in the baseline; nulls when the baseline has none.</summary>
    public static ActivityVariance Of(ScheduleActivity activity, BaselineActivity? reference)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return reference is null
            ? ActivityVariance.None
            : new ActivityVariance(
                WorkingDays.Variance(reference.PlannedStartDate, activity.ForecastStartDate),
                WorkingDays.Variance(reference.PlannedFinishDate, activity.ForecastFinishDate));
    }

    /// <summary>A milestone's forecast against its date in the baseline; null when the baseline has none.</summary>
    public static int? Of(ProjectMilestone milestone, BaselineMilestone? reference)
    {
        ArgumentNullException.ThrowIfNull(milestone);
        return reference is null ? null : WorkingDays.Variance(reference.PlannedDate, milestone.ForecastDate);
    }

    /// <summary>The project's finish variance: forecast finish against the baseline's finish; null without either.</summary>
    public static int? ProjectFinish(ProjectBaseline? baseline, DateOnly? forecastFinish) =>
        baseline is null || forecastFinish is null ? null : WorkingDays.Variance(baseline.BaselineFinishDate, forecastFinish.Value);
}

internal sealed record ActivityVariance(int? StartVarianceDays, int? FinishVarianceDays)
{
    public static ActivityVariance None { get; } = new(null, null);
}
