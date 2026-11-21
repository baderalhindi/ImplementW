using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A permission as one of the user's roles gives it.</summary>
public sealed record UserRoleGrant(string RoleCode, DataScope DataScope);

/// <summary>One permission of the catalogue for one user: what their roles give, what was adjusted, and whether they hold it now.</summary>
public sealed record UserPermissionEntry(
    string PermissionCode,
    BilingualLabel Name,
    string PermissionGroup,
    bool IsPrivileged,
    IReadOnlyList<UserRoleGrant> FromRoles,
    PermissionOverrideEffect? OverrideEffect,
    DataScope? OverrideScope,
    NarrativeText? OverrideReason,
    bool IsHeld);

public sealed record UserPermissions(Guid UserId, IReadOnlyList<UserPermissionEntry> Items);

/// <summary>A grant at a scope, or a removal, with why.</summary>
public sealed record UserPermissionOverrideChange(PermissionOverrideEffect Effect, DataScope? DataScope, NarrativeText Reason);

/// <summary>
/// Client review, item 5: start from a role, then grant or remove single permissions for one user. Unlike a permission profile's three
/// people, it applies at once, so it is held to step-up, a required reason, a PERMISSION_CHANGE audit event, no change to one's own access,
/// and <see cref="Common.Authorization.GrantPolicy"/>.
/// </summary>
public interface IUserPermissionOverrideService
{
    public Task<AdministrationResult<UserPermissions>> ListAsync(Guid actorId, Guid userId, CancellationToken cancellationToken);

    public Task<AdministrationResult<UserPermissions>> SetAsync(
        Guid actorId, Guid userId, string permissionCode, UserPermissionOverrideChange change, CancellationToken cancellationToken);

    public Task<AdministrationResult<UserPermissions>> ClearAsync(
        Guid actorId, Guid userId, string permissionCode, NarrativeText reason, CancellationToken cancellationToken);
}
