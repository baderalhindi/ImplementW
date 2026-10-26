namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// Who holds a role over a record's anchors now (E-U1): how WF-15 finds and rechecks the recipients a role-based routing
/// matrix names (TASK-039). A holder is an active person with an assignment in force, bound to a PUBLISHED profile
/// version whose role is one of those asked for, and whose anchors cover the record: each anchor the assignment carries
/// (project, department, entity) equals the record's. ADR-013 bounds an external person as the authorization engine
/// does: only through R04 or R08, only while their entity is active, and only for a record of their own entity.
/// </summary>
public interface IRoleHolderDirectory
{
    /// <summary>Every holder, each once, by user id.</summary>
    public Task<IReadOnlyList<RoleHolder>> FindHoldersAsync(IReadOnlyCollection<Guid> roleIds, RoleHolderScope scope, CancellationToken cancellationToken);

    /// <summary>The user, if they are a holder now; otherwise null.</summary>
    public Task<RoleHolder?> FindHolderAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, RoleHolderScope scope, CancellationToken cancellationToken);
}
