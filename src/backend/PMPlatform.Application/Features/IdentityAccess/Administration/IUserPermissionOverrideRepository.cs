using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>A permission a role assignment in force gives the user.</summary>
public sealed record RoleGrantHeld(string PermissionCode, string RoleCode, DataScope DataScope);

public interface IUserPermissionOverrideRepository
{
    /// <summary>The roles of the user's assignments in force, as the engine counts them.</summary>
    public Task<IReadOnlyList<string>> ListRoleCodesAsync(Guid userId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<RoleGrantHeld>> ListRolePermissionsAsync(Guid userId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<UserPermissionOverride>> ListAsync(Guid userId, CancellationToken cancellationToken);

    public void Add(UserPermissionOverride permissionOverride);

    public void Remove(UserPermissionOverride permissionOverride);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
