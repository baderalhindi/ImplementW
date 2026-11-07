using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The IdentityAccess tables as sign-in reads and writes them (TASK-028, TASK-029, TASK-068).</summary>
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

    public Task<LocalCredential?> FindLocalCredentialAsync(string username, CancellationToken cancellationToken) =>
        LocalCredentialAsync(u => u.Username == username, cancellationToken);

    public Task<LocalCredential?> FindLocalCredentialByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        LocalCredentialAsync(u => u.Id == userId, cancellationToken);

    public async Task<DirectoryAccount?> FindDirectoryAccountAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId && u.AccountSource == AccountSource.Directory && u.DirectorySubjectId != null)
            .Select(u => new DirectoryAccount(u.Username, u.DirectorySubjectId!))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

    public async Task RecordDirectoryPasswordChangedAsync(Guid userId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset changedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        await context.Set<User>().Where(u => u.Id == userId).ExecuteUpdateAsync(
            set => set
                .SetProperty(u => u.PasswordChangedAt, changedAt)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, userId),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailedPasswordAsync(Guid userId, int maxAttempts, DateTimeOffset lockedUntil, CancellationToken cancellationToken)
    {
        // One statement, so two wrong passwords at once both count.
        await context.Set<User>().Where(u => u.Id == userId).ExecuteUpdateAsync(
            set => set
                .SetProperty(u => u.LockedUntil, u => u.FailedSignInCount + 1 >= maxAttempts ? lockedUntil : u.LockedUntil)
                .SetProperty(u => u.FailedSignInCount, u => u.FailedSignInCount + 1 >= maxAttempts ? 0 : u.FailedSignInCount + 1),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearFailedPasswordsAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<User>().Where(u => u.Id == userId && (u.FailedSignInCount != 0 || u.LockedUntil != null)).ExecuteUpdateAsync(
            set => set.SetProperty(u => u.FailedSignInCount, 0).SetProperty(u => u.LockedUntil, (DateTimeOffset?)null),
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<string>> ListPasswordHistoryAsync(Guid userId, int count, CancellationToken cancellationToken) =>
        await context.Set<UserPasswordHistory>().AsNoTracking().Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt).Take(count).Select(h => h.PasswordHash)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task ChangePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken)
    {
        User user = await context.Set<User>().SingleAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        DateTimeOffset now = timeProvider.GetUtcNow();
        user.PasswordHash = passwordHash;
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        user.FailedSignInCount = 0;
        user.LockedUntil = null;
        user.UpdatedAt = now;
        user.UpdatedBy = userId;
        context.Add(new UserPasswordHistory { Id = Guid.CreateVersion7(now), UserId = userId, PasswordHash = passwordHash, CreatedAt = now, CreatedBy = userId, UpdatedAt = now, UpdatedBy = userId });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryClaimPasswordResetAsync(Guid userId, DateTimeOffset now, DateTimeOffset notSince, CancellationToken cancellationToken) =>
        await context.Set<User>()
            .Where(u => u.Id == userId && (u.PasswordResetRequestedAt == null || u.PasswordResetRequestedAt < notSince))
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.PasswordResetRequestedAt, now), cancellationToken).ConfigureAwait(false) == 1;

    private async Task<LocalCredential?> LocalCredentialAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken) =>
        await context.Set<User>().AsNoTracking().Where(predicate)
            .Where(u => u.AccountSource == AccountSource.Local && u.PasswordHash != null)
            .Select(u => new LocalCredential(u.Id, u.Username, u.Email, u.PasswordHash!, u.MustChangePassword, u.FailedSignInCount, u.LockedUntil))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);

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

    public async Task RecordMultiFactorEnrolmentAsync(Guid userId, CancellationToken cancellationToken)
    {
        User? user = await context.Set<User>().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.MfaEnrolledAt is not null)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.MfaEnrolledAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordIdentityVerificationAsync(Guid userId, string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(reference);

        User? user = await context.Set<User>().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.NafathVerifiedAt is not null)
        {
            return;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.NafathVerificationReference = reference;
        user.NafathVerifiedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = userId;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DirectoryLinkedPerson>> ListDirectoryLinkedPeopleAsync(CancellationToken cancellationToken) =>
        await (from user in context.Set<User>().AsNoTracking()
               where user.UserType == UserType.Internal && user.Status == UserStatus.Active && user.DirectorySubjectId != null
               join department in context.Set<Department>() on user.DepartmentId equals department.Id into departments
               from department in departments.DefaultIfEmpty()
               join manager in context.Set<User>() on user.ManagerUserId equals manager.Id into managers
               from manager in managers.DefaultIfEmpty()
               orderby user.Id
               select new DirectoryLinkedPerson(
                   user.Id, user.DirectorySubjectId!, user.JobTitle, department == null ? null : department.DirectoryReference,
                   manager == null ? null : manager.DirectorySubjectId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    private async Task<UserAccess?> FindAsync(Expression<Func<User, bool>> predicate, CancellationToken cancellationToken)
    {
        var user = await context.Set<User>().AsNoTracking()
            .Where(predicate)
            .Select(u => new { u.Id, u.UserType, u.Status, u.AccountEndsOn, u.ExternalEntityId, u.Username, u.DisplayName, u.PreferredLanguage, u.MfaEnrolledAt, u.NafathVerifiedAt, u.PasswordChangedAt })
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

        // An account past its end date is treated as disabled on every path (R4-6).
        UserStatus status = user.AccountEndsOn is { } endsOn && endsOn < DateOnly.FromDateTime(now.UtcDateTime) ? UserStatus.Disabled : user.Status;
        return new UserAccess(
            user.Id, user.UserType, status, entityStatus, user.Username, user.DisplayName, user.PreferredLanguage, user.MfaEnrolledAt is not null,
            user.NafathVerifiedAt is not null, assignments, user.PasswordChangedAt);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Directory {Attribute} of user {UserId} not applied: {Reason}. The stored value stands.")]
    private static partial void LogUnresolved(ILogger logger, Guid userId, string attribute, string reason);
}
