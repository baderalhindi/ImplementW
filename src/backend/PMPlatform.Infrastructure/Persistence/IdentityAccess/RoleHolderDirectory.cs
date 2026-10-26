using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>
/// Role holders over a record's anchors (TASK-039), read as the authorization engine reads grants (AuthorizationRepository):
/// assignments in force, bound to a PUBLISHED version, of an active person; an external person only through an
/// external-eligible role, while their entity is active, and only for their own entity's records (ADR-013).
/// </summary>
internal sealed class RoleHolderDirectory(PMPlatformDbContext context, TimeProvider timeProvider) : IRoleHolderDirectory
{
    public async Task<IReadOnlyList<RoleHolder>> FindHoldersAsync(IReadOnlyCollection<Guid> roleIds, RoleHolderScope scope, CancellationToken cancellationToken) =>
        await Holders(roleIds, scope, userId: null).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<RoleHolder?> FindHolderAsync(Guid userId, IReadOnlyCollection<Guid> roleIds, RoleHolderScope scope, CancellationToken cancellationToken) =>
        Holders(roleIds, scope, userId).FirstOrDefaultAsync(cancellationToken);

    private IQueryable<RoleHolder> Holders(IReadOnlyCollection<Guid> roleIds, RoleHolderScope scope, Guid? userId)
    {
        ArgumentNullException.ThrowIfNull(roleIds);
        ArgumentNullException.ThrowIfNull(scope);

        List<Guid> roles = [.. roleIds];
        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid? projectId = scope.ProjectId;
        Guid? departmentId = scope.DepartmentId;
        Guid? entityId = scope.ExternalEntityId;

        IQueryable<User> users = context.Set<User>().AsNoTracking();
        if (userId is { } only)
        {
            users = users.Where(u => u.Id == only);
        }

        return (
                from assignment in context.Set<AccessRelationship>().AsNoTracking()
                join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
                join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
                join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
                join user in users on assignment.UserId equals user.Id
                where roles.Contains(role.Id)
                      && assignment.Status == AccessRelationshipStatus.Active
                      && assignment.StartsAt <= now
                      && (assignment.EndsAt == null || assignment.EndsAt > now)
                      && version.LifecycleState == GovernedLifecycleState.Published
                      && user.Status == UserStatus.Active
                      && user.UserType != UserType.Service
                      && (assignment.ProjectId == null || assignment.ProjectId == projectId)
                      && (assignment.DepartmentId == null || assignment.DepartmentId == departmentId)
                      && (assignment.ExternalEntityId == null || assignment.ExternalEntityId == entityId)
                      && (user.UserType != UserType.External
                          || (role.IsExternalEligible
                              && entityId != null
                              && user.ExternalEntityId == entityId
                              && context.Set<ExternalEntity>().Any(e => e.Id == user.ExternalEntityId && e.Status == ExternalEntityStatus.Active)))
                select new { user.Id, user.Email, user.PreferredLanguage })
            .Distinct()
            .OrderBy(h => h.Id)
            .Select(h => new RoleHolder(h.Id, h.Email, h.PreferredLanguage));
    }
}
