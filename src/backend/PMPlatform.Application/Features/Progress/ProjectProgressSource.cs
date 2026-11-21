using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// Progress's answer to <see cref="IProjectProgressSource"/> (review round 8, point 02): the latest published stage-based period of each project,
/// the schedule's figure as WF-02 rolls it up (<see cref="ProgressRollup"/>), and which projects have an approved update. It reads the work breakdown
/// and the baseline directly rather than through <see cref="IProgressInputs"/>, which also reads the Project module's own figures.
/// </summary>
internal sealed class ProjectProgressSource(
    IProgressRepository repository,
    IProjectWorkBreakdownReader workBreakdown,
    IActiveBaselinePlanReader baselines) : IProjectProgressSource
{
    public async Task<IReadOnlyDictionary<Guid, PublishedStageProgress>> LatestPublishedStagesAsync(
        IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken)
    {
        if (projectIds.Count == 0)
        {
            return new Dictionary<Guid, PublishedStageProgress>();
        }

        IReadOnlyList<PublishedProgressSnapshot> latest = await repository.ListLatestSnapshotsAsync(projectIds, cancellationToken).ConfigureAwait(false);
        ILookup<Guid, ProgressStageEntry> stages = await repository.ReadStagesAsync([.. latest.Select(s => s.ProgressSubmissionId)], cancellationToken).ConfigureAwait(false);
        return latest
            .Where(s => stages[s.ProgressSubmissionId].Any())
            .ToDictionary(
                s => s.ProjectId,
                s => new PublishedStageProgress(
                    DateOnly.FromDateTime(s.PublishedAt.UtcDateTime),
                    [.. stages[s.ProgressSubmissionId].Select(e => new PublishedStageFigure(e.StageCode, e.WeightPercent, e.Applies, e.PercentComplete))]));
    }

    public async Task<IReadOnlyDictionary<Guid, ScheduleProgressFigure>> ScheduleProgressAsync(
        IReadOnlyCollection<Guid> projectIds, DateOnly asOf, CancellationToken cancellationToken)
    {
        Dictionary<Guid, ScheduleProgressFigure> figures = [];
        foreach (Guid projectId in projectIds)
        {
            if (await baselines.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } plan)
            {
                continue;
            }

            IReadOnlyList<WorkBreakdownItem> items = await workBreakdown.ListAsync(projectId, cancellationToken).ConfigureAwait(false);
            figures[projectId] = new ScheduleProgressFigure(
                ProgressRollup.Actual([.. items.Select(i => new WorkItemProgress(i.Id, i.ParentId, i.PlannedDurationDays, i.ActualPercentComplete ?? 0))]),
                ProgressRollup.Planned(new BaselinePlan(plan.BaselineId, [.. plan.Activities.Select(a => new BaselineActivity(a.Id, a.PlannedStart, a.PlannedFinish))]), asOf));
        }

        return figures;
    }

    public async Task<IReadOnlySet<Guid>> WithApprovedUpdateAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        projectIds.Count == 0
            ? new HashSet<Guid>()
            : [.. (await repository.ListLatestSnapshotsAsync(projectIds, cancellationToken).ConfigureAwait(false)).Select(s => s.ProjectId)];
}
