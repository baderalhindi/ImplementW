using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>Roles, the permission catalogue and the permission profiles, as ADM-006–009 read them.</summary>
public interface IRoleAdministrationRepository
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);

    public Task<Versioned<RoleDetail>?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken);

    public Task<Role?> FindRoleForUpdateAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken);

    public Task<IReadOnlyList<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken);

    public Task<PermissionProfilePage> ListProfilesAsync(Guid? baseRoleId, PageRequest page, CancellationToken cancellationToken);

    public Task<PermissionProfileDetail?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
