using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>
/// SCR-114 / MOD-043: standing delegations. A delegation conveys only the delegator's own authority at decision time,
/// never authority the delegator holds through a delegation, so no chain of delegations widens what anyone may decide.
/// </summary>
public interface IApprovalDelegationService
{
    public Task<ApprovalDelegationList> ListAsync(Guid callerId, CancellationToken cancellationToken);

    public Task<AdministrationResult<ApprovalDelegationDetail>> CreateAsync(Guid callerId, ApprovalDelegationDraft draft, CancellationToken cancellationToken);

    /// <summary>By the delegator, from now on.</summary>
    public Task<AdministrationResult<ApprovalDelegationDetail>> RevokeAsync(Guid callerId, Guid delegationId, CancellationToken cancellationToken);
}
