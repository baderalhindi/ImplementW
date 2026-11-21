using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Sign-in with biometrics (13 Nov 2026 review): the person gives their National ID or Iqama number, then proves it is
/// them with the passkey on their device (Face ID, Touch ID or the device unlock) or with a code by SMS to the mobile
/// number they verified. A passkey is the device in hand plus the person's biometric, so it passes both factors; an SMS
/// code proves only the phone, so a person who requires MFA still gives their authenticator code. At the client's request
/// the National ID is checked plainly (registered or not), and the first time on a device the person also types the mobile
/// number on their profile. Both end in <see cref="ContinueSignInAsync"/>, like every other sign-in.
/// </summary>
internal sealed partial class AuthenticationService
{
    public bool IsPasskeySignInAvailable => passkeyRelyingParty.IsConfigured;

    /// <summary>
    /// With a National ID, only its owner's passkey will verify (<see cref="Guid.Empty"/> when it is nobody's); without one,
    /// any registered passkey signs its own owner in, as the device offers it.
    /// </summary>
    public async Task<PasskeyCeremony?> BeginPasskeySignInAsync(string? nationalId, CancellationToken cancellationToken) =>
        !IsPasskeySignInAvailable ? null
        : nationalId is null ? passkeyRelyingParty.BeginAssertion(expectedUserId: null)
        : passkeyRelyingParty.BeginAssertion(await UserOfNationalIdAsync(nationalId, cancellationToken).ConfigureAwait(false) ?? Guid.Empty);

    public async Task<AuthenticationResult> SignInWithPasskeyAsync(string transaction, string credentialJson, CancellationToken cancellationToken)
    {
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.Passkey);
        return !IsPasskeySignInAvailable
            ? await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false)
            : await WithRejectionFloorAsync(
                async () =>
                {
                    // The relying party accepts the passkey only if it is the one the National ID's owner registered.
                    StoredPasskey? stored = passkeyRelyingParty.CredentialIdOf(credentialJson) is { } credentialId
                        ? await passkeys.FindAsync(credentialId, cancellationToken).ConfigureAwait(false)
                        : null;
                    long? counter = stored is null
                        ? null
                        : await passkeyRelyingParty.VerifyAssertionAsync(transaction, credentialJson, stored, cancellationToken).ConfigureAwait(false);
                    if (stored is null || counter is null)
                    {
                        return await FailAsync(attempt, AuthenticationFailureReason.CredentialsRejected, stored?.UserId).ConfigureAwait(false);
                    }

                    UserAccess? access = await users.FindByIdAsync(stored.UserId, cancellationToken).ConfigureAwait(false);
                    if (access is not { MaySignIn: true })
                    {
                        return await FailAsync(attempt, AuthenticationFailureReason.AccountInactive, stored.UserId).ConfigureAwait(false);
                    }

                    await passkeys.RecordUseAsync(stored.Id, counter.Value, cancellationToken).ConfigureAwait(false);
                    return await ContinueSignInAsync(attempt, access, new SessionAuthentication(AuthenticationMethod.Passkey, MultiFactor: true, Now()))
                        .ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public bool IsSmsSignInAvailable => smsCodes.IsConfigured && nationalIds.IsConfigured;

    /// <summary>
    /// The first step of sign-in with biometrics (the client's decision, 13 Nov 2026): whether this National ID belongs to a
    /// person who may sign in. It is answered plainly, so it tells a caller whether a number has an account (AUTH-T35).
    /// </summary>
    public async Task<bool> IsNationalIdRegisteredAsync(string nationalId, CancellationToken cancellationToken) =>
        await SignInHolderOfAsync(nationalId, cancellationToken).ConfigureAwait(false) is not null;

    /// <summary>
    /// The first time on a device: sends a code to the mobile number on the profile of the National ID's owner, if the number
    /// the person typed is that one. The code proves they hold it.
    /// </summary>
    public async Task<SmsSignInCodeRequest> RequestSmsSignInCodeAsync(string nationalId, string mobileNumber, CancellationToken cancellationToken)
    {
        if (!IsSmsSignInAvailable)
        {
            return SmsSignInCodeRequest.Unavailable;
        }

        if (await SignInHolderOfAsync(nationalId, cancellationToken).ConfigureAwait(false) is not { } access)
        {
            return SmsSignInCodeRequest.Refused(SmsSignInCodeRefusal.NationalIdNotRegistered);
        }

        if (await users.FindMobileNumberAsync(access.UserId, cancellationToken).ConfigureAwait(false) != mobileNumber)
        {
            LogMobileNumberMismatch(logger, access.UserId);
            return SmsSignInCodeRequest.Refused(SmsSignInCodeRefusal.MobileNumberMismatch);
        }

        SmsCodeSending sending = await smsCodes.SendAsync(access.UserId, mobileNumber, SmsCodePurpose.SignIn, access.PreferredLanguage, cancellationToken)
            .ConfigureAwait(false);
        return sending switch
        {
            { ChallengeId: { } challengeId, ExpiresAt: { } expiresAt } => SmsSignInCodeRequest.Sent(new SmsSignInChallenge(challengeId, expiresAt)),
            { IsTooSoon: true } => SmsSignInCodeRequest.Refused(SmsSignInCodeRefusal.RecentlySent),
            _ => SmsSignInCodeRequest.Unavailable,
        };
    }

    public async Task<AuthenticationResult> SignInWithSmsCodeAsync(Guid challengeId, string code, CancellationToken cancellationToken)
    {
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.SmsCode);
        return !IsSmsSignInAvailable
            ? await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false)
            : await WithRejectionFloorAsync(
                async () =>
                {
                    SmsCode? sent = await smsCodes.VerifyAsync(challengeId, SmsCodePurpose.SignIn, code, cancellationToken).ConfigureAwait(false);
                    if (sent is null)
                    {
                        return await FailAsync(attempt, AuthenticationFailureReason.CredentialsRejected, userId: null).ConfigureAwait(false);
                    }

                    // The number must still be the person's, and the person still allowed in. The code proved they hold the
                    // number, so it is now verified (ADR-004).
                    UserAccess? access = await users.FindByIdAsync(sent.UserId, cancellationToken).ConfigureAwait(false);
                    string? current = await users.FindMobileNumberAsync(sent.UserId, cancellationToken).ConfigureAwait(false);
                    if (access is not { MaySignIn: true } || current != sent.MobileNumber)
                    {
                        return await FailAsync(attempt, AuthenticationFailureReason.AccountInactive, sent.UserId).ConfigureAwait(false);
                    }

                    await users.RecordMobileNumberVerifiedAsync(sent.UserId, sent.MobileNumber, cancellationToken).ConfigureAwait(false);
                    return await ContinueSignInAsync(attempt, access, verifiedByIdentityProvider: null).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<UserAccess?> SignInHolderOfAsync(string nationalId, CancellationToken cancellationToken) =>
        await UserOfNationalIdAsync(nationalId, cancellationToken).ConfigureAwait(false) is { } userId
        && await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false) is { MaySignIn: true } access
            ? access
            : null;

    /// <summary>The user whose National ID or Iqama number this is, found by its fingerprint; the number itself is never stored or compared.</summary>
    private async Task<Guid?> UserOfNationalIdAsync(string nationalId, CancellationToken cancellationToken) =>
        NationalIdNumber.IsValid(nationalId)
            && nationalIds.IsConfigured
            ? await users.FindByNationalIdFingerprintAsync(nationalIds.Fingerprint(nationalId), cancellationToken).ConfigureAwait(false)
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS sign-in code not sent: the mobile number given is not the one on user {UserId}'s profile.")]
    private static partial void LogMobileNumberMismatch(ILogger logger, Guid userId);
}
