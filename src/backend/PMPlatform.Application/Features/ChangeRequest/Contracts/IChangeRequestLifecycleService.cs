using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// A change request's way along its state machine (TASK-060): one command per edge (R-4), each answering with the request. Its
/// requester submits and withdraws it; review, implementation and closure are AHDA's, so internal users only (ADR-013). WF-11's
/// decision is applied by the outcome handler, not a command here; approval issues authorisations and changes no other module.
/// </summary>
public interface IChangeRequestLifecycleService
{
    /// <summary>DRAFT or RETURNED → SUBMITTED, complete for its change type; a RETURNED request comes back as the next revision. CHANGE_REQUEST_RAISE.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> SubmitAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SUBMITTED or RETURNED → WITHDRAWN. CHANGE_REQUEST_RAISE.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> WithdrawAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// SUBMITTED → UNDER_REVIEW: records the authoritative materiality evaluation of the revision, pinned, and starts its WF-11 run
    /// routed by the resulting band, in one transaction. CHANGE_REQUEST_REVIEW, internal users.
    /// </summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> StartReviewAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>APPROVED → IMPLEMENTATION: from now on its target modules may apply its authorisations. CHANGE_REQUEST_IMPLEMENT, internal users.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> StartImplementationAsync(
        Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>IMPLEMENTATION → IMPLEMENTED, once every authorisation has been applied. CHANGE_REQUEST_IMPLEMENT, internal users.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> MarkImplementedAsync(
        Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>IMPLEMENTED → CLOSED. CHANGE_REQUEST_IMPLEMENT, internal users.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> CloseAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);
}
