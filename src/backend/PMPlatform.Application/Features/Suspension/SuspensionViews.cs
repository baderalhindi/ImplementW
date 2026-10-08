using PMPlatform.Application.Features.Suspension.Contracts;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>The representations of WF-09's rows: a request with the suspension period it opened or ended, read for a page at once.</summary>
internal sealed class SuspensionViews(ISuspensionRepository repository)
{
    public async Task<SuspensionRequestDetail> DetailAsync(SuspensionRequest request, CancellationToken cancellationToken) =>
        (await DetailsAsync([request], cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<SuspensionRequestDetail>> DetailsAsync(IReadOnlyList<SuspensionRequest> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        IReadOnlyList<ActiveSuspension> suspensions = await repository.ListSuspensionsOfAsync([.. requests.Select(r => r.Id)], cancellationToken).ConfigureAwait(false);
        return [.. requests.Select(r => ToDetail(r, suspensions.SingleOrDefault(s => (r.RequestType == SuspensionRequestType.Suspend ? s.SuspensionRequestId : s.ResumptionRequestId) == r.Id)))];
    }

    public static ActiveSuspensionDetail ToDetail(ActiveSuspension s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new ActiveSuspensionDetail(s.Id, s.ProjectId, s.SuspensionRequestId, s.StartedAt, s.EndedAt, s.EndReason, s.ResumptionRequestId);
    }

    private static SuspensionRequestDetail ToDetail(SuspensionRequest r, ActiveSuspension? suspension) =>
        new(r.Id, r.ProjectId, r.RequestType, r.Status, r.RevisionNo, r.Reason, r.RequestedByUserId, r.SubmittedAt, r.RequestedEffectiveDate, r.PlannedResumptionDate,
            r.EffectedAt, suspension is null ? null : ToDetail(suspension), r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy);
}
