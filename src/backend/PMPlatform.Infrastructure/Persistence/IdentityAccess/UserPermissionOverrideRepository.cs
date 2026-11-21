using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>Client review, item 5, over <c>identity_access.user_permission_override</c>.</summary>
internal sealed class UserPermissionOverrideRepository(PMPlatformDbContext context, TimeProvider timeProvider) : IUserPermissionOverrideRepository
{
    public async Task<IReadOnlyList<string>> ListRoleCodesAsync(Guid userId, CancellationToken cancellationToken) =>
        await RolesInForce(userId).Select(r => r.Code).Distinct().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<RoleGrantHeld>> ListRolePermissionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return await (
                from assignment in context.Set<AccessRelationship>().AsNoTracking()
                join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
                join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
                join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
                join grant in context.Set<PermissionProfileGrant>() on version.Id equals grant.PermissionProfileVersionId
                join permission in context.Set<Permission>() on grant.PermissionId equals permission.Id
                where assignment.UserId == userId
                      && assignment.Status == AccessRelationshipStatus.Active
                      && assignment.StartsAt <= now
                      && (assignment.EndsAt == null || assignment.EndsAt > now)
                      && version.LifecycleState == GovernedLifecycleState.Published
                select new RoleGrantHeld(permission.Code, role.Code, grant.DataScope))
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserPermissionOverride>> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<UserPermissionOverride>().Where(o => o.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(UserPermissionOverride permissionOverride) => context.Add(permissionOverride);

    public void Remove(UserPermissionOverride permissionOverride) => context.Remove(permissionOverride);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);

    private IQueryable<Role> RolesInForce(Guid userId)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return
            from assignment in context.Set<AccessRelationship>().AsNoTracking()
            join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
            join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
            join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
            where assignment.UserId == userId
                  && assignment.Status == AccessRelationshipStatus.Active
                  && assignment.StartsAt <= now
                  && (assignment.EndsAt == null || assignment.EndsAt > now)
                  && version.LifecycleState == GovernedLifecycleState.Published
            select role;
    }
}
