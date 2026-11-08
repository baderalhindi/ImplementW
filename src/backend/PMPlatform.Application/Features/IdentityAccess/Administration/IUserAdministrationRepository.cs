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

    public Task<Guid?> FindIdByUsernameAsync(string username, CancellationToken cancellationToken);

    /// <summary>The display name and entity of each user named, whatever their state; an id that names nobody is left out.</summary>
    /// <summary>Every ACTIVE user's identity, by username. Not tracked.</summary>
    public Task<IReadOnlyList<UserIdentity>> ListActiveIdentitiesAsync(CancellationToken cancellationToken);

    public Task<IReadOnlyList<UserIdentity>> ListActiveIdentitiesOfEntityAsync(Guid externalEntityId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<UserName>> ListNamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>The user holding this National ID fingerprint, whatever their status: a number belongs to one person.</summary>
    public Task<Guid?> FindIdByNationalIdFingerprintAsync(string fingerprint, CancellationToken cancellationToken);

    /// <summary>The sealed number of a user who is not deleted; null when there is none.</summary>
    public Task<string?> FindNationalIdSealedAsync(Guid userId, CancellationToken cancellationToken);

    public void Add(User user);

    public Task<SaveResult> SaveAsync(CancellationToken cancellationToken);
}
