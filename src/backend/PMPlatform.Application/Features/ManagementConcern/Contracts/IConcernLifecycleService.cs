using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.ManagementConcern.Contracts;

/// <summary>
/// A concern's way along its state machine (TASK-057): one command per edge (R-4), each answering with the concern. Every command
/// is management, so internal users only (ADR-013): an entity raises and sees, it does not manage. The assignee reaches its own
/// concern through an ASSIGNED grant. Validation is WF-11's decision, applied by the outcome handler, not a command here.
/// </summary>
public interface IConcernLifecycleService
{
    /// <summary>Replaces the concern's impacts and recomputes its severity under the RISK_MATRIX version in force, which it pins.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> AssessAsync(
        Guid callerId, Guid concernId, IReadOnlyList<ConcernImpactInput> impacts, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>OPEN → ASSIGNED; a reassignment keeps ASSIGNED or IN_PROGRESS.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> AssignAsync(
        Guid callerId, Guid concernId, Guid assigneeUserId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ASSIGNED → IN_PROGRESS.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> StartAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>IN_PROGRESS → PENDING_VALIDATION, with the resolution; starts its WF-11 validation run in the same transaction.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> SubmitResolutionAsync(
        Guid callerId, Guid concernId, NarrativeText resolution, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>Records a review of an open concern; the next is due at the governance profile's cadence (ADR-015).</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> ReviewAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>RESOLVED → CLOSED, with no OPEN escalation left.</summary>
    public Task<AdministrationResult<Versioned<ConcernDetail>>> CloseAsync(Guid callerId, Guid concernId, uint? expectedVersion, CancellationToken cancellationToken);
}
