using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>
/// A suspension or resumption request's way along its state machine (TASK-062): one command per edge (R-4), each answering with the
/// request. Its requester submits and withdraws it; review and activation are AHDA's, so internal users only (ADR-013). WF-11's decision
/// is applied by the outcome handler and makes the request APPROVED without touching the project; the project moves only when the
/// request is activated — by this service's command or, on the effective date, by WF-09's own pass.
/// </summary>
public interface ISuspensionLifecycleService
{
    /// <summary>DRAFT or RETURNED → SUBMITTED, with its effective date; a RETURNED request comes back as the next revision. SUSPENSION_RAISE.</summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> SubmitAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED or RETURNED → WITHDRAWN. SUSPENSION_RAISE.</summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> WithdrawAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW, starting the revision's WF-11 run in the same transaction. SUSPENSION_REVIEW, internal users.</summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> StartReviewAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// APPROVED → EFFECTED on or after its effective date: in one transaction the project moves ACTIVE → SUSPENDED and the active
    /// suspension opens, or SUSPENDED → ACTIVE and it ends. A refusal leaves the request APPROVED and the project as it was; the command is
    /// safe to retry. SUSPENSION_ACTIVATE, internal users.
    /// </summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> ActivateAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);
}
