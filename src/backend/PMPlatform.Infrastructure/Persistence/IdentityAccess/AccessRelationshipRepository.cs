using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>ADM-010 over <c>identity_access.access_relationship</c> (TASK-031).</summary>
internal sealed class AccessRelationshipRepository(PMPlatformDbContext context) : IAccessRelationshipRepository
{
    public async Task<AccessRelationshipPage> ListAsync(AccessRelationshipQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<AccessRelationship> assignments = context.Set<AccessRelationship>().AsNoTracking();
        if (query.UserId is { } userId)
        {
            assignments = assignments.Where(a => a.UserId == userId);
        }

        if (query.ProjectId is { } projectId)
        {
            assignments = assignments.Where(a => a.ProjectId == projectId);
        }

        if (query.Statuses.Count > 0)
        {
            assignments = assignments.Where(a => query.Statuses.Contains(a.Status));
        }

        int totalCount = await assignments.CountAsync(cancellationToken).ConfigureAwait(false);
        List<AccessRelationshipSummary> items = await (
                from assignment in assignments
                join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
                join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
                join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
                orderby assignment.StartsAt descending, assignment.Id
                select new AccessRelationshipSummary(
                    assignment.Id, assignment.UserId, role.Code, assignment.PermissionProfileVersionId, assignment.DepartmentId, assignment.ExternalEntityId,
                    assignment.ProjectId, assignment.SponsorUserId, assignment.StartsAt, assignment.EndsAt, assignment.EndReason, assignment.Status))
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AccessRelationshipPage(items, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public Task<AccessRelationshipDetail?> FindDetailAsync(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        (from assignment in context.Set<AccessRelationship>().AsNoTracking()
         join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
         join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
         join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
         where assignment.Id == accessRelationshipId
         select new AccessRelationshipDetail(
             assignment.Id, assignment.UserId, role.Code, profile.Id, profile.Code, version.Id, version.VersionNo, assignment.DepartmentId,
             assignment.ExternalEntityId, assignment.ProjectId, assignment.SponsorUserId, assignment.StartsAt, assignment.EndsAt, assignment.EndReason,
             assignment.Status, assignment.CreatedAt, assignment.CreatedBy, assignment.UpdatedAt, assignment.UpdatedBy))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<AccessRelationship?> FindForUpdateAsync(Guid accessRelationshipId, CancellationToken cancellationToken) =>
        context.Set<AccessRelationship>().SingleOrDefaultAsync(a => a.Id == accessRelationshipId, cancellationToken);

    public async Task<IReadOnlyList<AccessRelationship>> FindActiveOfUserForUpdateAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<AccessRelationship>()
            .Where(a => a.UserId == userId && a.Status == AccessRelationshipStatus.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<AccessRelationship>> FindActiveOnProjectForUpdateAsync(Guid projectId, CancellationToken cancellationToken) =>
        await context.Set<AccessRelationship>()
            .Where(a => a.ProjectId == projectId && a.Status == AccessRelationshipStatus.Active)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<ProfileVersionFacts?> FindProfileVersionAsync(Guid permissionProfileVersionId, CancellationToken cancellationToken) =>
        (from version in context.Set<PermissionProfileVersion>().AsNoTracking()
         join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
         join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
         where version.Id == permissionProfileVersionId
         select new ProfileVersionFacts(version.LifecycleState, role.Code, role.IsExternalEligible))
        .SingleOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Reads the Project module's row directly: there is no Project contract to ask yet (TASK-041). The foreign key
    /// <c>access_relationship.project_id</c> already couples the two tables (TASK-025).
    /// </summary>
    public Task<ProjectFacts?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        context.Set<ProjectEntity>().AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new ProjectFacts(p.ExternalEntityId, p.LifecycleState == ProjectLifecycleState.Closed))
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(AccessRelationship accessRelationship) => context.Set<AccessRelationship>().Add(accessRelationship);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);
}
