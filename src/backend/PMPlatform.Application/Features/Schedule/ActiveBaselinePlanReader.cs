using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <inheritdoc cref="IActiveBaselinePlanReader"/>
internal sealed class ActiveBaselinePlanReader(IScheduleRepository repository) : IActiveBaselinePlanReader
{
    public async Task<ActiveBaselinePlan?> FindAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await repository.FindActiveBaselineAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is not { } baseline)
        {
            return null;
        }

        IReadOnlyList<BaselineActivity> activities = await repository.ListBaselineActivitiesAsync(baseline.Id, cancellationToken).ConfigureAwait(false);
        List<ActiveBaselineActivity> leaves = [.. activities
            .Where(a => a.ActivityKind != ScheduleActivityKind.Summary)
            .Select(a => new ActiveBaselineActivity(a.Id, a.PlannedStartDate, a.PlannedFinishDate))];
        return leaves.Count == 0 ? null : new ActiveBaselinePlan(baseline.Id, leaves);
    }
}
