using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// WF-03 serving WF-08's port (<see cref="IApprovedBaselineSource"/>, edge 11): the project's ACTIVE Approved Baseline as a change request
/// is evaluated against it and a REBASELINE authorisation pins it. Its duration runs from the earliest planned start it froze to its finish.
/// </summary>
internal sealed class ApprovedBaselineSource(IScheduleRepository repository) : IApprovedBaselineSource
{
    public async Task<ApprovedBaselineFacts?> FindActiveAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await repository.FindActiveBaselineAsync(projectId, track: false, cancellationToken).ConfigureAwait(false) is not { BaselineType: BaselineType.Approved } active)
        {
            return null;
        }

        IReadOnlyList<BaselineActivity> activities = await repository.ListBaselineActivitiesAsync(active.Id, cancellationToken).ConfigureAwait(false);
        DateOnly start = activities.Count == 0 ? active.BaselineFinishDate : activities.Min(a => a.PlannedStartDate);
        return new ApprovedBaselineFacts(active.Id, active.VersionNo, start, active.BaselineFinishDate);
    }
}
