using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>The <c>identity_access.user</c> rows as ADM-002–005 read and write them.</summary>
public interface IUserAdministrationRepository
{
    public Task<UserPage> ListAsync(UserQuery query, CancellationToken cancellationToken);

    public Task<Versioned<UserDetail>?> FindDetailAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>The user, tracked. With <paramref name="expectedVersion"/>, saving fails as a concurrency conflict if the row has moved on from it.</summary>
    public Task<User?> FindForUpdateAsync(Guid userId, uint? expectedVersion, CancellationToken cancellationToken);

    public void Add(User user);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
