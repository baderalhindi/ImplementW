namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-002–005 and MOD-080 (TASK-031): the user lifecycle. A user is never deleted (ERD: RETAIN). Disabling changes the
/// user's status and nothing else, so every record they owned, were assigned or decided keeps naming them (Appendix A.1).
/// Every method is authorised by the engine for the acting user on the record it touches.
/// </summary>
public interface IUserAdministrationService
{
    public Task<AdministrationResult<UserPage>> ListAsync(Guid actorId, UserQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<UserDetail>>> GetAsync(Guid actorId, Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<UserDetail>>> CreateAsync(Guid actorId, UserDraft draft, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<UserDetail>>> UpdateAsync(
        Guid actorId, Guid userId, UserChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>DISABLED → ACTIVE. <paramref name="expectedVersion"/> is honoured when present (R-21).</summary>
    public Task<AdministrationResult<Versioned<UserDetail>>> ActivateAsync(Guid actorId, Guid userId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>ACTIVE → DISABLED. The user can neither sign in nor pass the engine from the next request on.</summary>
    public Task<AdministrationResult<Versioned<UserDetail>>> DisableAsync(Guid actorId, Guid userId, uint? expectedVersion, CancellationToken cancellationToken);
}
