using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The <c>identity_access.user_passkey</c> table (13 Nov 2026 review).</summary>
internal sealed class PasskeyRepository(PMPlatformDbContext context, TimeProvider timeProvider) : IPasskeyRepository
{
    public async Task<PasskeyList> ListAsync(Guid userId, CancellationToken cancellationToken) =>
        new(await context.Set<UserPasskey>().AsNoTracking().Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PasskeySummary(p.Id, p.Name, p.CreatedAt, p.LastUsedAt))
            .ToListAsync(cancellationToken).ConfigureAwait(false));

    public async Task<IReadOnlyList<byte[]>> ListCredentialIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Set<UserPasskey>().AsNoTracking().Where(p => p.UserId == userId).Select(p => p.CredentialId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<StoredPasskey?> FindAsync(byte[] credentialId, CancellationToken cancellationToken) =>
        context.Set<UserPasskey>().AsNoTracking().Where(p => p.CredentialId == credentialId)
            .Select(p => new StoredPasskey(p.Id, p.UserId, p.CredentialId, p.PublicKey, p.SignatureCounter))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PasskeySummary> AddAsync(Guid userId, NewPasskeyCredential credential, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(credential);
        DateTimeOffset now = timeProvider.GetUtcNow();
        UserPasskey passkey = new()
        {
            Id = Guid.CreateVersion7(now),
            UserId = userId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignatureCounter = credential.SignatureCounter,
            Name = name,
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId,
        };
        context.Add(passkey);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PasskeySummary(passkey.Id, passkey.Name, passkey.CreatedAt, LastUsedAt: null);
    }

    public async Task RecordUseAsync(Guid passkeyId, long signatureCounter, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await context.Set<UserPasskey>().Where(p => p.Id == passkeyId).ExecuteUpdateAsync(
            set => set.SetProperty(p => p.SignatureCounter, signatureCounter).SetProperty(p => p.LastUsedAt, now).SetProperty(p => p.UpdatedAt, now),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken) =>
        await context.Set<UserPasskey>().Where(p => p.Id == passkeyId && p.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false) == 1;
}
