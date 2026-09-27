using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>ADM-002–005 over <c>identity_access.user</c> (TASK-031).</summary>
internal sealed class UserAdministrationRepository(PMPlatformDbContext context) : IUserAdministrationRepository
{
    public async Task<UserPage> ListAsync(UserQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<User> users = context.Set<User>().AsNoTracking();
        if (query.Statuses.Count > 0)
        {
            users = users.Where(u => query.Statuses.Contains(u.Status));
        }

        if (query.UserTypes.Count > 0)
        {
            users = users.Where(u => query.UserTypes.Contains(u.UserType));
        }

        if (query.DepartmentId is { } departmentId)
        {
            users = users.Where(u => u.DepartmentId == departmentId);
        }

        if (query.ExternalEntityId is { } entityId)
        {
            users = users.Where(u => u.ExternalEntityId == entityId);
        }

        if (!string.IsNullOrWhiteSpace(query.Text))
        {
            string pattern = AdministrationPersistence.ContainsPattern(query.Text.Trim());
            users = users.Where(u => EF.Functions.ILike(u.Username, pattern) || EF.Functions.ILike(u.DisplayName, pattern) || EF.Functions.ILike(u.Email, pattern));
        }

        int totalCount = await users.CountAsync(cancellationToken).ConfigureAwait(false);

        // indexing-strategy I-08 serves the default: status filter, display name, id.
        IOrderedQueryable<User> ordered = query.Sort switch
        {
            UserSort.DisplayNameDescending => users.OrderByDescending(u => u.DisplayName).ThenByDescending(u => u.Id),
            UserSort.UsernameAscending => users.OrderBy(u => u.Username).ThenBy(u => u.Id),
            UserSort.UsernameDescending => users.OrderByDescending(u => u.Username).ThenByDescending(u => u.Id),
            UserSort.DisplayNameAscending => users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id),
            _ => users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id),
        };
        List<UserSummary> items = await ordered
            .Skip(query.Page.Skip)
            .Take(query.Page.PageSize)
            .Select(u => new UserSummary(u.Id, u.UserType, u.Username, u.DisplayName, u.Email, u.Status, u.DepartmentId, u.ExternalEntityId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new UserPage(items, query.Page.Page, query.Page.PageSize, totalCount);
    }

    public async Task<Versioned<UserDetail>?> FindDetailAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await context.Set<User>().AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { User = u, Version = EF.Property<uint>(u, EntityTypeBuilderExtensions.RowVersion) })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        User user = row.User;
        return new Versioned<UserDetail>(
            new UserDetail(
                user.Id, user.UserType, user.DirectorySubjectId, user.Username, user.DisplayName, user.Email, user.MobileNumber, user.MobileVerifiedAt,
                user.JobTitle, user.DepartmentId, user.ManagerUserId, user.ExternalEntityId, LanguageCode.Of(user.PreferredLanguage), user.Status,
                user.DisabledAt, user.MfaEnrolledAt is not null, user.NafathVerifiedAt, user.CreatedAt, user.CreatedBy, user.UpdatedAt, user.UpdatedBy),
            row.Version);
    }

    public async Task<User?> FindForUpdateAsync(Guid userId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        User? user = await context.Set<User>().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken).ConfigureAwait(false);
        if (user is not null)
        {
            AdministrationPersistence.ExpectVersion(context, user, expectedVersion);
        }

        return user;
    }

    public void Add(User user) => context.Set<User>().Add(user);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken) => AdministrationPersistence.SaveAsync(context, cancellationToken);
}
