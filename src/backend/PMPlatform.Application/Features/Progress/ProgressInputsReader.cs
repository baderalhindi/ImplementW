using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Progress;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Progress;

/// <summary>Reads WF-02's inputs from the modules that own them (ADR-003 §8.2 edges 1, 55, 56 and <see cref="IProjectFinancialStatusSource"/>).</summary>
internal sealed class ProgressInputsReader(
    IProjectWorkBreakdownReader workBreakdown,
    IActiveBaselinePlanReader baselines,
    IScheduleHealthReader scheduleHealth,
    IProjectFinancialStatusSource financialStatus,
    IProjectMonthlyUpdates declared) : IProgressInputs
{
    public async Task<ProgressInputs> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IReadOnlyList<WorkBreakdownItem> items = await workBreakdown.ListAsync(projectId, cancellationToken).ConfigureAwait(false);
        ActiveBaselinePlan? plan = await baselines.FindAsync(projectId, cancellationToken).ConfigureAwait(false);
        ScheduleHealthStatusDetail? schedule = await scheduleHealth.GetAsync(projectId, cancellationToken).ConfigureAwait(false);
        HealthStatus financial = await financialStatus.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        ProjectMonthlySnapshot? project = await declared.ReadAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new ProgressInputs(
            [.. items.Select(i => new WorkItemProgress(i.Id, i.ParentId, i.PlannedDurationDays, i.ActualPercentComplete ?? 0))],
            plan is null ? null : new BaselinePlan(plan.BaselineId, [.. plan.Activities.Select(a => new BaselineActivity(a.Id, a.PlannedStart, a.PlannedFinish))]),
            schedule?.ScheduleHealth switch
            {
                null => null,
                ScheduleHealth.Green => HealthStatus.Green,
                ScheduleHealth.Amber => HealthStatus.Amber,
                ScheduleHealth.Red => HealthStatus.Red,
                ScheduleHealth.Unknown => HealthStatus.Unknown,
                _ => throw new InvalidOperationException("Unknown schedule health."),
            },
            financial,
            project?.DeclaredActualPercent);
    }
}
