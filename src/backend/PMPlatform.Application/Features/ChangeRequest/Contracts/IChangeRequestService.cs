using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ChangeRequest.Contracts;

/// <summary>
/// WF-08's register (TASK-060): change requests raised by the project's people — an entity Project Manager included (ADR-013) — and
/// read back. A collection is of one project and is empty for one the caller may not see; a request the caller may not see is 404
/// (R-47). A request's state moves only through <see cref="IChangeRequestLifecycleService"/>.
/// </summary>
public interface IChangeRequestService
{
    public Task<ChangeRequestPage> ListAsync(Guid callerId, ChangeRequestQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> GetAsync(Guid callerId, Guid changeRequestId, CancellationToken cancellationToken);

    /// <summary>Raises a DRAFT while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED. CHANGE_REQUEST_RAISE.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> CreateAsync(Guid callerId, ChangeRequestDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces the fields of a DRAFT or RETURNED request. Requires the caller's version (R-21). CHANGE_REQUEST_RAISE.</summary>
    public Task<AdministrationResult<Versioned<ChangeRequestDetail>>> UpdateAsync(
        Guid callerId, Guid changeRequestId, ChangeRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>Deletes a DRAFT never submitted (HARD_DRAFT). Null when deleted, or when there is nothing the caller may see to delete.</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid changeRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// The classification the request would get now, by the rule its review applies, before it is submitted or reviewed: advisory, and
    /// recorded nowhere (WF-08 §6.1). CHANGE_REQUEST_VIEW.
    /// </summary>
    public Task<AdministrationResult<MaterialityAssessment>> PreviewMaterialityAsync(Guid callerId, Guid changeRequestId, CancellationToken cancellationToken);
}
