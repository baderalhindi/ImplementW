using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project.FollowUp;

/// <summary>
/// RPMO's calculated columns (the 2 Nov 2026 review §2), on the declared progress a project carries until it has a schedule. The gap is
/// actual minus planned, in points; it is not calculated for a project not yet started (registration, APPROVED_PLANNED) or closed, nor when
/// either percentage is missing.
/// </summary>
internal static class ProjectIndicatorCalculator
{
    public static ProjectIndicators Of(ProjectEntity project, OnPlanThresholds thresholds, DateOnly today, UpdateComplianceCounts? updates = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(thresholds);

        bool running = project.LifecycleState is ProjectLifecycleState.Active or ProjectLifecycleState.Suspended;
        bool measured = running || project.LifecycleState == ProjectLifecycleState.Completed;
        decimal? gap = measured && project.DeclaredActualPercent is { } actual && project.DeclaredPlannedPercent is { } planned ? actual - planned : null;
        OnPlanStatus? onPlan = gap is not { } points ? null
            : points <= -thresholds.StalledPoints ? OnPlanStatus.Stalled
            : points <= -thresholds.LatePoints ? OnPlanStatus.Late
            : OnPlanStatus.OnTrack;
        int? daysRemaining = running && project.PlannedEndDate is { } end ? end.DayNumber - today.DayNumber : null;
        ActionDueStatus? actionDue = project.NextActionDueDate is not { } due || project.NextAction is null ? null
            : due < today ? ActionDueStatus.Overdue
            : ActionDueStatus.Scheduled;

        List<FollowUpReason> reasons = [];
        if (running)
        {
            if (onPlan == OnPlanStatus.Stalled)
            {
                reasons.Add(FollowUpReason.Stalled);
            }

            if (project.LifecycleState == ProjectLifecycleState.Suspended)
            {
                reasons.Add(FollowUpReason.Suspended);
            }

            if (onPlan == OnPlanStatus.Late)
            {
                reasons.Add(FollowUpReason.Late);
            }

            if (actionDue == ActionDueStatus.Overdue)
            {
                reasons.Add(FollowUpReason.ActionOverdue);
            }

            if (project.CurrentObstacle is not null)
            {
                reasons.Add(FollowUpReason.OpenObstacle);
            }
        }

        return new ProjectIndicators(gap, onPlan, daysRemaining, actionDue, reasons, Compliance(project, thresholds, updates));
    }

    /// <summary>RPMO's recorded value overrides the calculation; with neither, compliance is not known.</summary>
    private static EntityUpdateCompliance? Compliance(ProjectEntity project, OnPlanThresholds thresholds, UpdateComplianceCounts? updates)
    {
        decimal? percent = updates is { Counted: > 0 } counts ? decimal.Round(100m * counts.OnTime / counts.Counted, 1) : null;
        bool? calculated = percent is { } share ? share >= thresholds.CompliancePercent : null;
        return updates is null && project.EntityUpdateCompliant is null
            ? null
            : new EntityUpdateCompliance(
                updates?.Counted ?? 0, updates?.OnTime ?? 0, percent, calculated, project.EntityUpdateCompliant ?? calculated, project.EntityUpdateCompliant is not null);
    }
}
