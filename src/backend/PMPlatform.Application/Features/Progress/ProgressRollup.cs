namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// ADR-009 option C: actual progress captured at task level and rolled up through the work breakdown, weighted by planned
/// duration; planned progress calculated from the approved baseline. Rolling a summary up from its children, weighted by
/// their durations, and its parent up from it, weighted by the sum of those durations, comes to the same figure as weighting
/// every leaf by its own duration, so the project figure is computed from the leaves. Figures are percentages 0–100,
/// rounded to the four places the columns hold.
/// </summary>
internal static class ProgressRollup
{
    private const int Places = 4;

    /// <summary>The project's actual percentage; null when the breakdown has no leaf with a planned duration, so nothing to weight.</summary>
    public static decimal? Actual(IReadOnlyList<WorkItemProgress> workBreakdown)
    {
        ArgumentNullException.ThrowIfNull(workBreakdown);

        HashSet<Guid> parents = [.. workBreakdown.Where(i => i.ParentId is not null).Select(i => i.ParentId!.Value)];
        decimal weighted = 0;
        decimal duration = 0;
        foreach (WorkItemProgress leaf in workBreakdown.Where(i => !parents.Contains(i.Id)))
        {
            ArgumentOutOfRangeException.ThrowIfNegative(leaf.PlannedDurationDays, nameof(workBreakdown));
            if (leaf.ActualPercent is < 0 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(workBreakdown), leaf.ActualPercent, $"Work item {leaf.Id}: actual percent must be 0–100.");
            }

            weighted += leaf.ActualPercent * leaf.PlannedDurationDays;
            duration += leaf.PlannedDurationDays;
        }

        return duration == 0 ? null : Round(weighted / duration);
    }

    /// <summary>
    /// The percentage of the baseline planned to be complete at the end of <paramref name="asOf"/>: each activity accrues
    /// linearly over its planned days, both ends included, weighted by its duration. Null without activities.
    /// </summary>
    public static decimal? Planned(BaselinePlan? baseline, DateOnly asOf)
    {
        if (baseline is null || baseline.Activities.Count == 0)
        {
            return null;
        }

        decimal elapsed = 0;
        decimal duration = 0;
        foreach (BaselineActivity activity in baseline.Activities)
        {
            int days = activity.PlannedFinish.DayNumber - activity.PlannedStart.DayNumber + 1;
            if (days < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(baseline), $"Baseline activity {activity.Id} finishes before it starts.");
            }

            elapsed += Math.Clamp(asOf.DayNumber - activity.PlannedStart.DayNumber + 1, 0, days);
            duration += days;
        }

        return Round(elapsed * 100 / duration);
    }

    private static decimal Round(decimal percent) => Math.Round(percent, Places, MidpointRounding.AwayFromZero);
}
