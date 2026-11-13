using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Sign-in with biometrics (13 Nov 2026 review): the person gives their National ID or Iqama number, then proves it is
/// them with the passkey on their device (Face ID, Touch ID or the device unlock) or with a code by SMS to the mobile
/// number they verified. A passkey is the device in hand plus the person's biometric, so it passes both factors; an SMS
/// code proves only the phone, so a person who requires MFA still gives their authenticator code. Every answer before
/// the proof is the same whether or not the number belongs to anyone, so the page cannot be used to learn who has an
/// account. Both end in <see cref="ContinueSignInAsync"/>, like every other sign-in.
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
    /// Sends a code to the verified mobile number of the person with this National ID, if they may sign in. The answer, and
    /// its timing, are the same either way.
    /// </summary>
    public async Task<SmsSignInChallenge?> RequestSmsSignInCodeAsync(string nationalId, CancellationToken cancellationToken)
    {
        if (!IsSmsSignInAvailable)
        {
            return null;
        }

        long startedAt = timeProvider.GetTimestamp();
        SmsCodeSending sending = await SendSmsSignInCodeAsync(nationalId, cancellationToken).ConfigureAwait(false);
        await WaitOutRejectionFloorAsync(startedAt, cancellationToken).ConfigureAwait(false);
        return sending.ChallengeId is { } challengeId
            ? new SmsSignInChallenge(challengeId, sending.ExpiresAt!.Value)
            : new SmsSignInChallenge(Guid.CreateVersion7(timeProvider.GetUtcNow()), timeProvider.GetUtcNow() + smsCodeOptions.Lifetime);
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

                    // The number must still be the person's and verified, and the person still allowed in.
                    UserAccess? access = await users.FindByIdAsync(sent.UserId, cancellationToken).ConfigureAwait(false);
                    string? verified = await users.FindVerifiedMobileNumberAsync(sent.UserId, cancellationToken).ConfigureAwait(false);
                    return access is { MaySignIn: true } && verified == sent.MobileNumber
                        ? await ContinueSignInAsync(attempt, access, verifiedByIdentityProvider: null).ConfigureAwait(false)
                        : await FailAsync(attempt, AuthenticationFailureReason.AccountInactive, sent.UserId).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SmsCodeSending> SendSmsSignInCodeAsync(string nationalId, CancellationToken cancellationToken)
    {
        if (await UserOfNationalIdAsync(nationalId, cancellationToken).ConfigureAwait(false) is not { } userId
            || await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false) is not { MaySignIn: true } access
            || await users.FindVerifiedMobileNumberAsync(userId, cancellationToken).ConfigureAwait(false) is not { } mobileNumber)
        {
            LogSmsSignInCodeNotSent(logger);
            return SmsCodeSending.Unavailable;
        }

        return await smsCodes.SendAsync(access.UserId, mobileNumber, SmsCodePurpose.SignIn, access.PreferredLanguage, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The user whose National ID or Iqama number this is, found by its fingerprint; the number itself is never stored or compared.</summary>
    private async Task<Guid?> UserOfNationalIdAsync(string nationalId, CancellationToken cancellationToken) =>
        NationalIdNumber.IsValid(nationalId)
            && nationalIds.IsConfigured
            ? await users.FindByNationalIdFingerprintAsync(nationalIds.Fingerprint(nationalId), cancellationToken).ConfigureAwait(false)
            : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS sign-in code not sent: no user who may sign in has that National ID and a verified mobile number.")]
    private static partial void LogSmsSignInCodeNotSent(ILogger logger);
}
