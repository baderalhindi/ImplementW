using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.ChangeRequest.Contracts;
using PMPlatform.Application.Features.ChangeRequest.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// WF-08's side of edges 11 and 12 (<see cref="IChangeAuthorizations"/>): a target module checks an authorisation before it accepts a
/// change and applies it as the change takes effect. Applying writes nothing but the authorisation and its audit event, staged in the
/// target module's unit of work: they commit with the change or not at all (M-11), and the authorisation's row version refuses a
/// concurrent second application at that save.
/// </summary>
internal sealed class ChangeAuthorizationLedger(IChangeRequestRepository repository, IProjectFactsReader projects, IAuditTrail audit, TimeProvider timeProvider)
    : IChangeAuthorizations
{
    public async Task<ChangeAuthorizationVerdict> CheckAsync(ChangeAuthorizationClaim claim, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        (ChangeAuthorization? authorization, ChangeRequestEntity? request) = await FindAsync(claim, track: false, cancellationToken).ConfigureAwait(false);
        return ChangeAuthorizationRules.Judge(authorization, request, claim, null, timeProvider.GetUtcNow());
    }

    public async Task<ChangeAuthorizationVerdict> ApplyAsync(ChangeAuthorizationClaim claim, ChangeAuthorizationUse use, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(use);
        (ChangeAuthorization? authorization, ChangeRequestEntity? request) = await FindAsync(claim, track: true, cancellationToken).ConfigureAwait(false);
        ChangeAuthorizationVerdict verdict = ChangeAuthorizationRules.Judge(authorization, request, claim, use.AppliedReference, use.AppliedAt);
        if (verdict != ChangeAuthorizationVerdict.Applicable)
        {
            return verdict;
        }

        authorization!.Status = ChangeAuthorizationStatus.Applied;
        authorization.AppliedAt = use.AppliedAt;
        authorization.AppliedByUserId = use.ActorUserId;
        authorization.AppliedReference = use.AppliedReference;
        ChangeRequestGate.Touch(authorization, use.ActorUserId, use.AppliedAt);

        ProjectFacts project = await projects.FindAsync(request!.ProjectId, cancellationToken).ConfigureAwait(false)
                               ?? throw new InvalidOperationException($"Change request {request.Id} names no project.");
        audit.Stage(ChangeRequestAudit.Authorization(ChangeRequestAuditEvents.ChangeAuthorizationApplied, use.ActorUserId, project, request, authorization));
        return ChangeAuthorizationVerdict.Applied;
    }

    private async Task<(ChangeAuthorization? Authorization, ChangeRequestEntity? Request)> FindAsync(
        ChangeAuthorizationClaim claim, bool track, CancellationToken cancellationToken)
    {
        ChangeAuthorization? authorization = await repository.FindAuthorizationAsync(claim.ChangeAuthorizationId, track, cancellationToken).ConfigureAwait(false);
        return (authorization, authorization is null ? null : await repository.FindAsync(authorization.ChangeRequestId, null, cancellationToken).ConfigureAwait(false));
    }
}
