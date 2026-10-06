using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// WF-07's register (TASK-057): issues and challenges, raised by the project's people — an entity on its own project included
/// (ADR-013: intake and visibility) — and read back. A collection is of one project and is empty for one the caller may not see;
/// a concern the caller may not see is 404 (R-47). A concern's state moves only through <see cref="IConcernLifecycleService"/>.
/// </summary>
public interface IManagementConcernService
{
    public Task<ConcernPage> ListAsync(Guid callerId, ConcernQuery query, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ConcernDetail>>> GetAsync(Guid callerId, Guid concernId, CancellationToken cancellationToken);

    /// <summary>
    /// Raises an issue or a challenge, OPEN, while the project is APPROVED_PLANNED, ACTIVE or SUSPENDED. Its severity is computed from
    /// its impacts, if any are given; its next review is due at the cadence of the project's governance profile (ADR-015). CONCERN_RAISE.
    /// </summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> RaiseAsync(Guid callerId, ConcernDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces the fields of a concern not yet with validation. Requires the caller's version (R-21). CONCERN_MANAGE, internal users.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> UpdateAsync(
        Guid callerId, Guid concernId, ConcernChanges changes, uint expectedVersion, CancellationToken cancellationToken);
}
