using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>A project's open baseline candidates and unachieved milestones, for WF-10's readiness (edge 40).</summary>
internal sealed class ScheduleCloseoutReader(IScheduleRepository repository) : IScheduleCloseoutReader
{
    public async Task<ScheduleCloseoutPosition> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProjectBaseline> baselines = await repository.ListBaselinesAsync(projectId, track: false, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ProjectMilestone> milestones = await repository.FindScheduleAsync(projectId, cancellationToken).ConfigureAwait(false) is { } schedule
            ? await repository.ListMilestonesAsync(schedule.Id, cancellationToken).ConfigureAwait(false)
            : [];
        return new ScheduleCloseoutPosition(baselines.Count(b => BaselineWorkflow.IsOpen(b.Status)), milestones.Count(m => m.Status == ProjectMilestoneStatus.Planned));
    }
}
