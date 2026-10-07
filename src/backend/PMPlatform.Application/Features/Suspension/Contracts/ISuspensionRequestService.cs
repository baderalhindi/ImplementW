using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Suspension.Contracts;

/// <summary>
/// WF-09's register (TASK-062): suspension and resumption requests raised by the project's people — an entity Project Manager included
/// (ADR-013) — and the project's suspension periods. A collection is of one project and is empty for one the caller may not see; a
/// request the caller may not see is 404 (R-47). A request's state moves only through <see cref="ISuspensionLifecycleService"/>.
/// </summary>
public interface ISuspensionRequestService
{
    public Task<SuspensionRequestPage> ListAsync(Guid callerId, SuspensionRequestQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> GetAsync(Guid callerId, Guid suspensionRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Raises a DRAFT: a suspension of an ACTIVE project with no open suspension request, or a resumption of a SUSPENDED project with no
    /// open resumption request. SUSPENSION_RAISE.
    /// </summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> CreateAsync(Guid callerId, SuspensionRequestDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces the fields of a DRAFT or RETURNED request. Requires the caller's version (R-21). SUSPENSION_RAISE.</summary>
    public Task<AdministrationResult<Versioned<SuspensionRequestDetail>>> UpdateAsync(
        Guid callerId, Guid suspensionRequestId, SuspensionRequestChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>Deletes a DRAFT never submitted (HARD_DRAFT). Null when deleted, or when there is nothing the caller may see to delete.</summary>
    public Task<AdministrationError?> DeleteAsync(Guid callerId, Guid suspensionRequestId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's suspension periods, open and ended. SUSPENSION_VIEW.</summary>
    public Task<ActiveSuspensionPage> ListSuspensionsAsync(Guid callerId, ActiveSuspensionQuery query, PageRequest page, CancellationToken cancellationToken);
}
