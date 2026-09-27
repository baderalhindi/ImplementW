using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>ADM-006–009 over roles, the permission catalogue and the permission profiles (TASK-031).</summary>
internal sealed class RoleAdministrationRepository(PMPlatformDbContext context) : IRoleAdministrationRepository
{
    public async Task<IReadOnlyList<RoleSummary>> ListRolesAsync(CancellationToken cancellationToken) =>
        await context.Set<Role>().AsNoTracking()
            .OrderBy(r => r.Code)
            .Select(r => new RoleSummary(r.Id, r.Code, r.Name, r.IsSystem, r.IsExternalEligible))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Versioned<RoleDetail>?> FindRoleAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var row = await context.Set<Role>().AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => new { Role = r, Version = EF.Property<uint>(r, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        List<PermissionProfileSummary> profiles = await ProfileSummaries(p => p.BaseRoleId == roleId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        Role role = row.Role;
        return new Versioned<RoleDetail>(
            new RoleDetail(role.Id, role.Code, role.Name, role.IsSystem, role.IsExternalEligible, profiles, role.UpdatedAt, role.UpdatedBy),
            row.Version);
    }

    public async Task<Role?> FindRoleForUpdateAsync(Guid roleId, uint expectedVersion, CancellationToken cancellationToken)
    {
        Role? role = await context.Set<Role>().SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken).ConfigureAwait(false);
        if (role is not null)
        {
            AdministrationPersistence.ExpectVersion(context, role, expectedVersion);
        }

        return role;
    }

    public async Task<IReadOnlyList<PermissionSummary>> ListPermissionsAsync(CancellationToken cancellationToken) =>
        await context.Set<Permission>().AsNoTracking()
            .OrderBy(p => p.PermissionGroup).ThenBy(p => p.Code)
            .Select(p => new PermissionSummary(p.Id, p.Code, p.Name, p.PermissionGroup, p.IsPrivileged, p.DataClassificationItemId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PermissionProfilePage> ListProfilesAsync(Guid? baseRoleId, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        Expression<Func<PermissionProfile, bool>> filter = baseRoleId is { } roleId ? p => p.BaseRoleId == roleId : p => true;
        int totalCount = await context.Set<PermissionProfile>().CountAsync(filter, cancellationToken).ConfigureAwait(false);
        List<PermissionProfileSummary> items = await ProfileSummaries(filter)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PermissionProfilePage(items, page.Page, page.PageSize, totalCount);
    }

    public async Task<PermissionProfileDetail?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        PermissionProfileSummary? profile = await ProfileSummaries(p => p.Id == profileId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        var versions = await context.Set<PermissionProfileVersion>().AsNoTracking()
            .Where(v => v.PermissionProfileId == profileId)
            .OrderByDescending(v => v.VersionNo)
            .Select(v => new { v.Id, v.VersionNo, v.LifecycleState, v.PublishedAt, v.RetiredAt })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        List<Guid> versionIds = [.. versions.Select(v => v.Id)];
        var grants = await (
                from grant in context.Set<PermissionProfileGrant>().AsNoTracking()
                join permission in context.Set<Permission>() on grant.PermissionId equals permission.Id
                where versionIds.Contains(grant.PermissionProfileVersionId)
                orderby permission.Code
                select new { grant.PermissionProfileVersionId, Grant = new PermissionProfileGrantDetail(permission.Id, permission.Code, grant.DataScope) })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        ILookup<Guid, PermissionProfileGrantDetail> grantsByVersion = grants.ToLookup(g => g.PermissionProfileVersionId, g => g.Grant);

        return new PermissionProfileDetail(
            profile.Id, profile.Code, profile.Name, profile.BaseRoleId, profile.BaseRoleCode, profile.IsShippedDefault,
            [.. versions.Select(v => new PermissionProfileVersionDetail(v.Id, v.VersionNo, v.LifecycleState, v.PublishedAt, v.RetiredAt, [.. grantsByVersion[v.Id]]))]);
    }

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);

    /// <summary>The profiles <paramref name="filter"/> selects, by code. The filter applies before the projection, which EF cannot filter.</summary>
    private IQueryable<PermissionProfileSummary> ProfileSummaries(Expression<Func<PermissionProfile, bool>> filter) =>
        from profile in context.Set<PermissionProfile>().AsNoTracking().Where(filter)
        join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
        orderby profile.Code, profile.Id
        select new PermissionProfileSummary(profile.Id, profile.Code, profile.Name, profile.BaseRoleId, role.Code, profile.IsShippedDefault);
}
