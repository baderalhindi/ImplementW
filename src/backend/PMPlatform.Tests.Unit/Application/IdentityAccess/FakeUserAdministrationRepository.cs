using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

internal sealed class FakeUserAdministrationRepository : FakeStore<User>, IUserAdministrationRepository
{
    public Task<UserPage> ListAsync(UserQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(PageOf(
            Rows.Values.Where(u => query.Statuses.Count == 0 || query.Statuses.Contains(u.Status)).OrderBy(u => u.DisplayName, StringComparer.Ordinal)
                .Select(u => new UserSummary(u.Id, u.UserType, u.Username, u.DisplayName, u.Email, u.Status, u.DepartmentId, u.ExternalEntityId)),
            query.Page,
            (items, total) => new UserPage(items, query.Page.Page, query.Page.PageSize, total)));

    public Task<Versioned<UserDetail>?> FindDetailAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.TryGetValue(userId, out User? u)
            ? new Versioned<UserDetail>(
                new UserDetail(
                    u.Id, u.UserType, u.DirectorySubjectId, u.Username, u.DisplayName, u.Email, u.MobileNumber, u.MobileVerifiedAt, u.JobTitle, u.DepartmentId,
                    u.ManagerUserId, u.ExternalEntityId, LanguageCode.Of(u.PreferredLanguage), u.Status, u.DisabledAt, u.MfaEnrolledAt is not null, u.NafathVerifiedAt,
                    u.CreatedAt, u.CreatedBy, u.UpdatedAt, u.UpdatedBy),
                Versions[u.Id])
            : null);

    public Task<User?> FindForUpdateAsync(Guid userId, uint? expectedVersion, CancellationToken cancellationToken) =>
        Task.FromResult(Track(userId, expectedVersion));
}
