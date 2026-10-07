using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Domain.ChangeRequest;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>The representations of WF-08's rows: a request with its latest recorded evaluation and its authorisations, read for a page at once.</summary>
internal sealed class ChangeRequestViews(IChangeRequestRepository repository)
{
    public async Task<ChangeRequestDetail> DetailAsync(ChangeRequestEntity request, CancellationToken cancellationToken) =>
        (await DetailsAsync([request], cancellationToken).ConfigureAwait(false))[0];

    public async Task<IReadOnlyList<ChangeRequestDetail>> DetailsAsync(IReadOnlyList<ChangeRequestEntity> requests, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requests);
        Guid[] ids = [.. requests.Select(r => r.Id)];
        Dictionary<Guid, MaterialityEvaluation> evaluations = (await repository.ListLatestEvaluationsAsync(ids, cancellationToken).ConfigureAwait(false))
            .ToDictionary(e => e.ChangeRequestId);
        ILookup<Guid, ChangeAuthorization> authorizations = (await repository.ListAuthorizationsAsync(ids, cancellationToken).ConfigureAwait(false))
            .ToLookup(a => a.ChangeRequestId);
        return
        [
            .. requests.Select(r => new ChangeRequestDetail(
                r.Id, r.ProjectId, r.Title, r.Justification, r.ChangeType, r.Status, r.RevisionNo, r.RequestedByUserId, r.SubmittedAt, r.CostImpactSar,
                r.ScheduleImpactDays, r.ScopeImpact, r.IsContractualObligation, r.RequestedGovernanceProfileItemId,
                evaluations.TryGetValue(r.Id, out MaterialityEvaluation? evaluation) ? MaterialityEvaluator.ToAssessment(evaluation, recorded: true) : null,
                [.. authorizations[r.Id].Select(ToDetail)],
                r.ImplementedAt, r.ClosedAt, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy)),
        ];
    }

    public static ChangeAuthorizationDetail ToDetail(ChangeAuthorization a) =>
        new(a.Id, a.ChangeRequestId, a.ApprovalInstanceId, a.AuthorizationScope, a.TargetModule, a.TargetType, a.TargetId, a.TargetRevisionNo, a.Status, a.IssuedAt,
            a.ExpiresAt, a.AppliedAt, a.AppliedByUserId, a.AppliedReference);
}
