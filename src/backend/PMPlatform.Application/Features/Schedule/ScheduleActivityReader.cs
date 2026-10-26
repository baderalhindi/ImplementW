using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>A schedule activity as WF-03 holds it, for WF-04 (edge 9).</summary>
internal sealed class ScheduleActivityReader(IScheduleRepository repository) : IScheduleActivityReader
{
    public async Task<ScheduleActivityFacts?> FindAsync(Guid activityId, CancellationToken cancellationToken) =>
        await repository.ReadActivityAsync(activityId, cancellationToken).ConfigureAwait(false) is ({ } a, Guid projectId)
            ? new ScheduleActivityFacts(a.Id, projectId, a.ActivityKind, a.Status, a.PlannedStartDate, a.PlannedFinishDate, a.PlannedDurationDays)
            : null;
}
