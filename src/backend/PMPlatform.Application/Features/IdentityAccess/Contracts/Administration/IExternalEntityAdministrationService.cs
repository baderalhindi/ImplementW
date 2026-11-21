using PMPlatform.Application.Common.Deletion;
namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-013 Entities (TASK-031, ADR-013). ACTIVE ↔ SUSPENDED, and either → RETIRED, which is terminal. A suspended or
/// retired entity's people can neither sign in nor act; their assignments and records are left as they are.
/// </summary>
public interface IExternalEntityAdministrationService
{
    public Task<AdministrationResult<ExternalEntityPage>> ListAsync(Guid actorId, ExternalEntityQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> GetAsync(Guid actorId, Guid entityId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> CreateAsync(Guid actorId, ExternalEntityDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> UpdateAsync(
        Guid actorId, Guid entityId, ExternalEntityChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> SuspendAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> ActivateAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ExternalEntityDetail>>> RetireAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Erases an entity nothing references (the client wants Delete, not Retire). It is cleared from no record: a project would lose who delivers it, a
    /// person the entity they belong to, and a role assignment the entity it is limited to, which would widen what that person reaches. So while
    /// projects, people, assignments, update requests or the audit trail reference it, nothing is deleted and the answer lists them.
    /// </summary>
    public Task<AdministrationResult<DeletionOutcome>> DeleteAsync(Guid actorId, Guid entityId, uint? expectedVersion, CancellationToken cancellationToken);
}
