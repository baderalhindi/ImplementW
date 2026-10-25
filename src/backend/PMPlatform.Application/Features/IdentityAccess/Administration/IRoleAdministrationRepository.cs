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

    /// <summary>The latest PUBLISHED version of the profile with this code, if any.</summary>
    public Task<Guid?> FindPublishedProfileVersionIdAsync(string profileCode, CancellationToken cancellationToken);

    /// <summary>Every profile with a PUBLISHED version, by code.</summary>
    public Task<IReadOnlyList<PermissionProfileSummary>> ListAssignableProfilesAsync(CancellationToken cancellationToken);

    /// <summary>The profile, tracked (TASK-110).</summary>
    public Task<PermissionProfile?> FindProfileForUpdateAsync(Guid profileId, CancellationToken cancellationToken);

    public Task<bool> IsProfileCodeInUseAsync(string code, CancellationToken cancellationToken);

    /// <summary>The version, tracked. With <paramref name="expectedVersion"/>, saving fails as a concurrency conflict if the row has moved on from it.</summary>
    public Task<PermissionProfileVersion?> FindVersionForUpdateAsync(Guid versionId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The profile's versions, newest first, untracked.</summary>
    public Task<IReadOnlyList<PermissionProfileVersion>> ListVersionsAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>The version's grants, tracked.</summary>
    public Task<IReadOnlyList<PermissionProfileGrant>> ListGrantsForUpdateAsync(Guid versionId, CancellationToken cancellationToken);

    public Task<Versioned<PermissionProfileVersionDetail>?> FindVersionDetailAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>The active assignments bound to the version.</summary>
    public Task<int> CountActiveAssignmentsAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>Whether the user holds an active assignment of any version of the profile.</summary>
    public Task<bool> HoldsProfileAsync(Guid userId, Guid profileId, CancellationToken cancellationToken);

    /// <summary>The active assignments bound to the profile's versions other than <paramref name="exceptVersionId"/>, tracked.</summary>
    public Task<IReadOnlyList<AccessRelationship>> ListActiveAssignmentsForUpdateAsync(Guid profileId, Guid exceptVersionId, CancellationToken cancellationToken);

    public void Add(PermissionProfile profile);

    public void Add(PermissionProfileVersion version);

    public void Add(PermissionProfileGrant grant);

    public void Remove(PermissionProfileGrant grant);

    public uint RowVersionOf(PermissionProfileVersion version);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
