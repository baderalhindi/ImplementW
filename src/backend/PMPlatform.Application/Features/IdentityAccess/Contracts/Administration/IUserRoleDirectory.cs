namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// The canonical roles a person holds now, through the assignments in force their session carries, and whether they are an external
/// entity's (E-U1): how FG-01 decides which dashboards a person may open and lands on (Blueprint §20.2, ADR-019; TASK-069). A role selects a
/// dashboard; it never grants data (BR-DSH-008), which the authorization engine decides on each widget's own permission.
/// </summary>
public interface IUserRoleDirectory
{
    /// <summary>No roles for a person who may not hold a session. An external person holds only the roles ADR-013 lets them hold.</summary>
    public Task<UserRoles> FindAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Distinct role codes, in ordinal order, and whether the person is an external entity's (ADR-013).</summary>
public sealed record UserRoles(bool IsExternal, IReadOnlyList<string> RoleCodes)
{
    public static UserRoles None { get; } = new(false, []);
}
