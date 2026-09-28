using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Approval;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.Approval;

/// <summary>
/// SCR-114 / MOD-043. Giving a delegation checks only the people and the period: what it conveys is decided each time
/// the delegate acts, from the delegator's own authority then (<see cref="ApprovalAuthority"/>). Delegator and
/// delegate are active internal users (ADR-013).
/// </summary>
internal sealed class ApprovalDelegationService(IApprovalRepository repository, ApprovalAuthority authority, IAuditTrail audit, TimeProvider timeProvider)
    : IApprovalDelegationService
{
    public async Task<ApprovalDelegationList> ListAsync(Guid callerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApprovalDelegation> delegations = await repository.ListDelegationsAsync(callerId, cancellationToken).ConfigureAwait(false);
        return new ApprovalDelegationList(
            [.. delegations.Where(d => d.DelegatorUserId == callerId).Select(ApprovalMapping.ToDetail)],
            [.. delegations.Where(d => d.DelegateUserId == callerId).Select(ApprovalMapping.ToDetail)]);
    }

    public async Task<AdministrationResult<ApprovalDelegationDetail>> CreateAsync(Guid callerId, ApprovalDelegationDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (!await authority.IsActiveInternalAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Forbidden;
        }

        if (draft.DelegateUserId == callerId || !await authority.IsActiveInternalAsync(draft.DelegateUserId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.Rule(ApprovalErrorCodes.DelegateInvalid, new FieldIssue("delegateUserId", FieldIssue.NotAllowed));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset validFrom = draft.ValidFrom ?? now;
        if (validFrom < now)
        {
            return AdministrationError.Rule(ApprovalErrorCodes.DelegationPeriodInvalid, new FieldIssue("validFrom", FieldIssue.NotAllowed));
        }

        if (draft.ValidTo <= validFrom)
        {
            return AdministrationError.Rule(ApprovalErrorCodes.DelegationPeriodInvalid, new FieldIssue("validTo", FieldIssue.BeforeStart));
        }

        ApprovalDelegation delegation = new()
        {
            Id = Guid.CreateVersion7(now),
            DelegatorUserId = callerId,
            DelegateUserId = draft.DelegateUserId,
            RoutingKey = draft.RoutingKey,
            ValidFrom = validFrom,
            ValidTo = draft.ValidTo,
            Status = ApprovalDelegationStatus.Active,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        repository.Add(delegation);
        audit.Stage(ApprovalAudit.Delegation(ApprovalAuditEvents.DelegationCreated, callerId, AuditActorType.User, delegation));
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        return ApprovalMapping.ToDetail(delegation);
    }

    public async Task<AdministrationResult<ApprovalDelegationDetail>> RevokeAsync(Guid callerId, Guid delegationId, CancellationToken cancellationToken)
    {
        ApprovalDelegation? delegation = await repository.FindDelegationAsync(delegationId, cancellationToken).ConfigureAwait(false);
        if (delegation is null || (delegation.DelegatorUserId != callerId && delegation.DelegateUserId != callerId))
        {
            return AdministrationError.NotFound;
        }

        if (delegation.DelegatorUserId != callerId)
        {
            return AdministrationError.Forbidden;
        }

        if (delegation.Status != ApprovalDelegationStatus.Active)
        {
            return AdministrationError.TerminalState;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        delegation.Status = ApprovalDelegationStatus.Revoked;
        delegation.RevokedAt = now;
        ApprovalRows.Touch(delegation, callerId, now);
        audit.Stage(ApprovalAudit.Delegation(ApprovalAuditEvents.DelegationRevoked, callerId, AuditActorType.User, delegation));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == ApprovalSaveOutcome.Saved
            ? ApprovalMapping.ToDetail(delegation)
            : AdministrationError.PreconditionFailed;
    }
}
