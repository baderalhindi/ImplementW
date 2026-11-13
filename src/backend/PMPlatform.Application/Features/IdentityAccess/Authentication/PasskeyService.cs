using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// A person's own passkeys (13 Nov 2026 review). A passkey signs its holder in with both factors, so adding one needs a
/// sign-in within <see cref="RecentSignIn"/>: whoever adds it has just proved who they are, not merely kept a tab open.
/// Removing one needs only the session: it takes access away and gives none.
/// </summary>
internal sealed class PasskeyService(
    IPasskeyRelyingParty relyingParty,
    IPasskeyRepository passkeys,
    IUserAccessRepository users,
    IAuditTrail audit,
    TimeProvider timeProvider) : IPasskeyService
{
    /// <summary>How recent the sign-in must be to add a passkey.</summary>
    internal static readonly TimeSpan RecentSignIn = TimeSpan.FromMinutes(15);

    public bool IsAvailable => relyingParty.IsConfigured;

    public Task<PasskeyList> ListAsync(Guid userId, CancellationToken cancellationToken) => passkeys.ListAsync(userId, cancellationToken);

    public async Task<AdministrationResult<PasskeyCeremony>> BeginRegistrationAsync(Guid userId, SessionAuthentication session, CancellationToken cancellationToken)
    {
        if (await RefusalAsync(userId, session, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        UserAccess access = (await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false))!;
        IReadOnlyList<byte[]> registered = await passkeys.ListCredentialIdsAsync(userId, cancellationToken).ConfigureAwait(false);
        return relyingParty.BeginRegistration(new PasskeyHolder(userId, access.Username, access.DisplayName), registered);
    }

    public async Task<AdministrationResult<PasskeySummary>> RegisterAsync(
        Guid userId, SessionAuthentication session, string transaction, string credentialJson, string name, CancellationToken cancellationToken)
    {
        if (await RefusalAsync(userId, session, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        NewPasskeyCredential? credential = await relyingParty.CompleteRegistrationAsync(userId, transaction, credentialJson, cancellationToken).ConfigureAwait(false);
        if (credential is null || await passkeys.FindAsync(credential.CredentialId, cancellationToken).ConfigureAwait(false) is not null)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.PasskeyRejected);
        }

        PasskeySummary added = await passkeys.AddAsync(userId, credential, name.Trim(), cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(Entry(IdentityAccessAuditEvents.PasskeyRegistered, userId, added.Id)).ConfigureAwait(false);
        return added;
    }

    public async Task<AdministrationResult<PasskeyList>> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken cancellationToken)
    {
        if (!await passkeys.RemoveAsync(userId, passkeyId, cancellationToken).ConfigureAwait(false))
        {
            return AdministrationError.NotFound;
        }

        await audit.RecordAsync(Entry(IdentityAccessAuditEvents.PasskeyRemoved, userId, passkeyId)).ConfigureAwait(false);
        return await passkeys.ListAsync(userId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AdministrationError?> RefusalAsync(Guid userId, SessionAuthentication session, CancellationToken cancellationToken) =>
        !relyingParty.IsConfigured ? AdministrationError.Unavailable
        : await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false) is not { MaySignIn: true } ? AdministrationError.Forbidden
        : timeProvider.GetUtcNow() - session.AuthenticatedAt > RecentSignIn ? AdministrationError.Rule(IdentityAccessErrorCodes.RecentSignInRequired)
        : null;

    private static AuditEntry Entry(string eventType, Guid userId, Guid passkeyId) =>
        new(AuditEventClass.Authentication, eventType, AuditOutcome.Success)
        {
            ActorUserId = userId,
            Subject = new AuditSubject("IdentityAccess", nameof(UserPasskey), passkeyId),
        };
}
