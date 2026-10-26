using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
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

    public Task<Guid?> FindPublishedProfileVersionIdAsync(string profileCode, CancellationToken cancellationToken) =>
        context.Set<PermissionProfileVersion>().AsNoTracking()
            .Join(context.Set<PermissionProfile>(), v => v.PermissionProfileId, p => p.Id, (v, p) => new { Version = v, p.Code })
            .Where(x => x.Code == profileCode && x.Version.LifecycleState == GovernedLifecycleState.Published)
            .OrderByDescending(x => x.Version.VersionNo)
            .Select(x => (Guid?)x.Version.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PermissionProfileSummary>> ListAssignableProfilesAsync(CancellationToken cancellationToken) =>
        await ProfileSummaries(p => context.Set<PermissionProfileVersion>().Any(v => v.PermissionProfileId == p.Id && v.LifecycleState == GovernedLifecycleState.Published))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<PermissionProfileDetail?> FindProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        PermissionProfileSummary? profile = await ProfileSummaries(p => p.Id == profileId)
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        List<PermissionProfileVersion> versions = await context.Set<PermissionProfileVersion>().AsNoTracking()
            .Where(v => v.PermissionProfileId == profileId)
            .OrderByDescending(v => v.VersionNo)
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
            [.. versions.Select(v => DetailOf(v, [.. grantsByVersion[v.Id]]))]);
    }

    public Task<PermissionProfile?> FindProfileForUpdateAsync(Guid profileId, CancellationToken cancellationToken) =>
        context.Set<PermissionProfile>().SingleOrDefaultAsync(p => p.Id == profileId, cancellationToken);

    public Task<bool> IsProfileCodeInUseAsync(string code, CancellationToken cancellationToken) =>
        context.Set<PermissionProfile>().AsNoTracking().AnyAsync(p => p.Code == code, cancellationToken);

    public async Task<PermissionProfileVersion?> FindVersionForUpdateAsync(Guid versionId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await context.Set<PermissionProfileVersion>().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken).ConfigureAwait(false);
        if (version is not null && expectedVersion is { } expected)
        {
            AdministrationPersistence.ExpectVersion(context, version, expected);
        }

        return version;
    }

    public async Task<IReadOnlyList<PermissionProfileVersion>> ListVersionsAsync(Guid profileId, CancellationToken cancellationToken) =>
        await context.Set<PermissionProfileVersion>().AsNoTracking().Where(v => v.PermissionProfileId == profileId).OrderByDescending(v => v.VersionNo)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PermissionProfileGrant>> ListGrantsForUpdateAsync(Guid versionId, CancellationToken cancellationToken) =>
        await context.Set<PermissionProfileGrant>().Where(g => g.PermissionProfileVersionId == versionId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<Versioned<PermissionProfileVersionDetail>?> FindVersionDetailAsync(Guid versionId, CancellationToken cancellationToken)
    {
        PermissionProfileVersion? version = await context.Set<PermissionProfileVersion>().AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return null;
        }

        List<PermissionProfileGrantDetail> grants = await (
                from grant in context.Set<PermissionProfileGrant>().AsNoTracking()
                join permission in context.Set<Permission>() on grant.PermissionId equals permission.Id
                where grant.PermissionProfileVersionId == versionId
                orderby permission.Code
                select new PermissionProfileGrantDetail(permission.Id, permission.Code, grant.DataScope))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        uint rowVersion = await context.Set<PermissionProfileVersion>().AsNoTracking().Where(v => v.Id == versionId)
            .Select(v => EF.Property<uint>(v, EntityTypeBuilderExtensions.RowVersion)).SingleAsync(cancellationToken).ConfigureAwait(false);
        return new Versioned<PermissionProfileVersionDetail>(DetailOf(version, grants), rowVersion);
    }

    public Task<int> CountActiveAssignmentsAsync(Guid versionId, CancellationToken cancellationToken) =>
        context.Set<AccessRelationship>().AsNoTracking()
            .CountAsync(a => a.PermissionProfileVersionId == versionId && a.Status == AccessRelationshipStatus.Active, cancellationToken);

    public Task<bool> HoldsProfileAsync(Guid userId, Guid profileId, CancellationToken cancellationToken) =>
        (from assignment in context.Set<AccessRelationship>().AsNoTracking()
         join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
         where assignment.UserId == userId && assignment.Status == AccessRelationshipStatus.Active && version.PermissionProfileId == profileId
         select assignment.Id).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<AccessRelationship>> ListActiveAssignmentsForUpdateAsync(Guid profileId, Guid exceptVersionId, CancellationToken cancellationToken) =>
        await (from assignment in context.Set<AccessRelationship>()
               join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
               where assignment.Status == AccessRelationshipStatus.Active && version.PermissionProfileId == profileId && version.Id != exceptVersionId
               select assignment).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(PermissionProfile profile) => context.Set<PermissionProfile>().Add(profile);

    public void Add(PermissionProfileVersion version) => context.Set<PermissionProfileVersion>().Add(version);

    public void Add(PermissionProfileGrant grant) => context.Set<PermissionProfileGrant>().Add(grant);

    public void Remove(PermissionProfileGrant grant) => context.Set<PermissionProfileGrant>().Remove(grant);

    public uint RowVersionOf(PermissionProfileVersion version) =>
        context.Entry(version).Property<uint>(EntityTypeBuilderExtensions.RowVersion).CurrentValue;

    private static PermissionProfileVersionDetail DetailOf(PermissionProfileVersion v, IReadOnlyList<PermissionProfileGrantDetail> grants) =>
        new(v.Id, v.VersionNo, v.LifecycleState, v.PublishedAt, v.RetiredAt, grants)
        {
            PermissionProfileId = v.PermissionProfileId,
            ChangeSummary = v.ChangeSummary,
            CreatedBy = v.CreatedBy,
            ValidatedByUserId = v.ValidatedByUserId,
            PublishedByUserId = v.PublishedByUserId,
        };

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);

    /// <summary>The profiles <paramref name="filter"/> selects, by code. The filter applies before the projection, which EF cannot filter.</summary>
    private IQueryable<PermissionProfileSummary> ProfileSummaries(Expression<Func<PermissionProfile, bool>> filter) =>
        from profile in context.Set<PermissionProfile>().AsNoTracking().Where(filter)
        join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
        orderby profile.Code, profile.Id
        select new PermissionProfileSummary(profile.Id, profile.Code, profile.Name, profile.BaseRoleId, role.Code, profile.IsShippedDefault);
}
