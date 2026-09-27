using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>
/// ADM-006–009 (TASK-031). R01–R08 are canonical and undeletable (ERD F-080, CTL-09): a role is neither created nor deleted
/// here, and only its bilingual name is edited. What a role may do is a permission profile (ADR-018), authored and
/// versioned by TASK-110; ADM-009's matrix is the grants of each profile version, read here. The catalogue is protected
/// (ERD F-081) and read-only.
/// </summary>
public interface IRoleAdministrationService
{
    public Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<RoleDetail>>> GetRoleAsync(Guid roleId, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<RoleDetail>>> RenameRoleAsync(
        Guid actorId, Guid roleId, BilingualLabel name, uint expectedVersion, CancellationToken cancellationToken);

    public Task<IReadOnlyList<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken);

    public Task<PermissionProfilePage> ListProfilesAsync(Guid? baseRoleId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<PermissionProfileDetail>> GetProfileAsync(Guid profileId, CancellationToken cancellationToken);
}
