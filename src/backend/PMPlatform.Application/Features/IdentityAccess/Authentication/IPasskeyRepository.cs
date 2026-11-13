using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The <c>identity_access.user_passkey</c> table.</summary>
public interface IPasskeyRepository
{
    public Task<PasskeyList> ListAsync(Guid userId, CancellationToken cancellationToken);

    public Task<IReadOnlyList<byte[]>> ListCredentialIdsAsync(Guid userId, CancellationToken cancellationToken);

    public Task<StoredPasskey?> FindAsync(byte[] credentialId, CancellationToken cancellationToken);

    public Task<PasskeySummary> AddAsync(Guid userId, NewPasskeyCredential credential, string name, CancellationToken cancellationToken);

    public Task RecordUseAsync(Guid passkeyId, long signatureCounter, CancellationToken cancellationToken);

    /// <summary>True if the person had that passkey and it is gone.</summary>
    public Task<bool> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken);
}
