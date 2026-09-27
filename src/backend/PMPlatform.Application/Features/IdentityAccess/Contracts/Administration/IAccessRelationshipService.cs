namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-010 Role Assignment (TASK-031). An assignment is never deleted or rewritten: it ends, with a reason. A new
/// assignment on a project where the user already holds one ends the old one as ROLE_CHANGE (ADR-013). The engine reads
/// assignments on every request, so a change applies to the user's next request without a new sign-in.
/// </summary>
public interface IAccessRelationshipService
{
    public Task<AdministrationResult<AccessRelationshipPage>> ListAsync(Guid actorId, AccessRelationshipQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<AccessRelationshipDetail>> GetAsync(Guid actorId, Guid accessRelationshipId, CancellationToken cancellationToken);

    public Task<AdministrationResult<AccessRelationshipDetail>> CreateAsync(Guid actorId, AccessRelationshipDraft draft, CancellationToken cancellationToken);

    /// <summary>ACTIVE → ENDED, reason MANUAL.</summary>
    public Task<AdministrationResult<AccessRelationshipDetail>> EndAsync(Guid actorId, Guid accessRelationshipId, CancellationToken cancellationToken);
}
