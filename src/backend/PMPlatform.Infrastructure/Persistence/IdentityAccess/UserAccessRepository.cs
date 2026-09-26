using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The IdentityAccess tables as sign-in reads and writes them (TASK-028).</summary>
internal sealed partial class UserAccessRepository(PMPlatformDbContext context, TimeProvider timeProvider, ILogger<UserAccessRepository> logger)
    : IUserAccessRepository
{
    /// <summary>
    /// The SERVICE principal directory-sourced changes are attributed to (ERD D-2), seeded by db/seed. A department or
    /// manager copied from the directory is the directory's change, not the signing-in person's.
    /// </summary>
    public static readonly Guid DirectorySyncPrincipalId = new("00000000-0000-4000-8000-0000000000fe");

    public Task<UserAccess?> FindByDirectorySubjectAsync(string directorySubjectId, CancellationToken cancellationToken) =>
        FindAsync(u => u.DirectorySubjectId == directorySubjectId, cancellationToken);

    public Task<UserAccess?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        FindAsync(u => u.Id == userId, cancellationToken);

    public async Task ApplyDirectoryAttributesAsync(Guid userId, DirectoryEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);

        User? user = await context.Set<User>().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return;
        }

        Guid? departmentId = user.DepartmentId;
        if (entry.DepartmentReference is null)
        {
            departmentId = null;
        }
        else if (await context.Set<Department>().AsNoTracking()
                     .Where(d => d.DirectoryReference == entry.DepartmentReference && d.IsActive)
                     .Select(d => (Guid?)d.Id)
                     .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) is { } resolvedDepartment)
        {
            departmentId = resolvedDepartment;
        }
        else
        {
            LogUnresolved(logger, userId, "department", "no active department has that directory reference");
        }

        Guid? managerUserId = user.ManagerUserId;
        if (entry.ManagerSubjectId is null)
        {
            managerUserId = null;
        }
        else if (await context.Set<User>().AsNoTracking()
                     .Where(u => u.DirectorySubjectId == entry.ManagerSubjectId && u.Id != userId)
                     .Select(u => (Guid?)u.Id)
                     .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false) is { } resolvedManager)
        {
            managerUserId = resolvedManager;
        }
        else
        {
            LogUnresolved(logger, userId, "manager", "the manager has no platform user");
        }

        if (user.JobTitle == entry.JobTitle && user.DepartmentId == departmentId && user.ManagerUserId == managerUserId)
        {
            return;
        }

        user.JobTitle = entry.JobTitle;
        user.DepartmentId = departmentId;
        user.ManagerUserId = managerUserId;
        user.UpdatedAt = timeProvider.GetUtcNow();
        user.UpdatedBy = DirectorySyncPrincipalId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<UserAccess?> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken)
    {
        var user = await context.Set<User>().AsNoTracking()
            .Where(predicate)
            .Select(u => new { u.Id, u.UserType, u.Status, u.ExternalEntityId, u.Username, u.DisplayName, u.PreferredLanguage })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return null;
        }

        ExternalEntityStatus? entityStatus = user.ExternalEntityId is { } entityId
            ? await context.Set<ExternalEntity>().AsNoTracking()
                .Where(e => e.Id == entityId)
                .Select(e => (ExternalEntityStatus?)e.Status)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            : null;

        // Active now, bound to a profile version (ADR-018) whose base role is the canonical role. An external user's
        // assignment counts only for an external-eligible role, R04 or R08 (ADR-013): anything else is not honoured.
        DateTimeOffset now = timeProvider.GetUtcNow();
        bool external = user.UserType == UserType.External;
        List<SessionRoleAssignment> assignments = await (
                from assignment in context.Set<AccessRelationship>().AsNoTracking()
                join version in context.Set<PermissionProfileVersion>() on assignment.PermissionProfileVersionId equals version.Id
                join profile in context.Set<PermissionProfile>() on version.PermissionProfileId equals profile.Id
                join role in context.Set<Role>() on profile.BaseRoleId equals role.Id
                where assignment.UserId == user.Id
                      && assignment.Status == AccessRelationshipStatus.Active
                      && assignment.StartsAt <= now
                      && (assignment.EndsAt == null || assignment.EndsAt > now)
                      && (!external || role.IsExternalEligible)
                orderby role.Code, assignment.Id
                select new SessionRoleAssignment(role.Code, assignment.PermissionProfileVersionId, assignment.DepartmentId, assignment.ExternalEntityId, assignment.ProjectId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new UserAccess(user.Id, user.UserType, user.Status, entityStatus, user.Username, user.DisplayName, user.PreferredLanguage, assignments);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory {Attribute} of user {UserId} not applied: {Reason}. The stored value stands.")]
    private static partial void LogUnresolved(ILogger logger, Guid userId, string attribute, string reason);
}
