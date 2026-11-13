using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The two sign-ins without a password (13 Nov 2026 review). A passkey is the device in hand plus the person's Face ID,
/// Touch ID or device unlock, so it passes both factors at once. An SMS code proves only the phone, so it is a first
/// factor: a person who requires MFA still gives their authenticator code. Both end in <see cref="ContinueSignInAsync"/>,
/// like every other sign-in.
/// </summary>
internal sealed partial class AuthenticationService
{
    public bool IsPasskeySignInAvailable => passkeyRelyingParty.IsConfigured;

    public PasskeyCeremony? BeginPasskeySignIn() => passkeyRelyingParty.IsConfigured ? passkeyRelyingParty.BeginAssertion() : null;

    public async Task<AuthenticationResult> SignInWithPasskeyAsync(string transaction, string credentialJson, CancellationToken cancellationToken)
    {
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.Passkey);
        return !passkeyRelyingParty.IsConfigured
            ? await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false)
            : await WithRejectionFloorAsync(
                async () =>
                {
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

    public bool IsSmsSignInAvailable => smsCodes.IsConfigured;

    /// <summary>
    /// Sends a code if exactly one person who may sign in has verified this number. The answer, and its timing, are the
    /// same either way, so the page cannot be used to learn whose number is whose.
    /// </summary>
    public async Task<SmsSignInChallenge?> RequestSmsSignInCodeAsync(string mobileNumber, CancellationToken cancellationToken)
    {
        if (!smsCodes.IsConfigured)
        {
            return null;
        }

        long startedAt = timeProvider.GetTimestamp();
        SmsCodeSending sending = await SendSmsSignInCodeAsync(mobileNumber, cancellationToken).ConfigureAwait(false);
        await WaitOutRejectionFloorAsync(startedAt, cancellationToken).ConfigureAwait(false);
        return sending.ChallengeId is { } challengeId
            ? new SmsSignInChallenge(challengeId, sending.ExpiresAt!.Value)
            : new SmsSignInChallenge(Guid.CreateVersion7(timeProvider.GetUtcNow()), timeProvider.GetUtcNow() + smsCodeOptions.Lifetime);
    }

    public async Task<AuthenticationResult> SignInWithSmsCodeAsync(Guid challengeId, string code, CancellationToken cancellationToken)
    {
        Attempt attempt = Attempt.SignIn(AuthenticationMethod.SmsCode);
        return !smsCodes.IsConfigured
            ? await FailAsync(attempt, AuthenticationFailureReason.NotConfigured, userId: null).ConfigureAwait(false)
            : await WithRejectionFloorAsync(
                async () =>
                {
                    SmsCode? sent = await smsCodes.VerifyAsync(challengeId, SmsCodePurpose.SignIn, code, cancellationToken).ConfigureAwait(false);
                    if (sent is null)
                    {
                        return await FailAsync(attempt, AuthenticationFailureReason.CredentialsRejected, userId: null).ConfigureAwait(false);
                    }

                    // The number must still be the person's, verified, and the person still allowed in.
                    UserAccess? access = await users.FindByIdAsync(sent.UserId, cancellationToken).ConfigureAwait(false);
                    IReadOnlyList<Guid> holders = await users.FindByVerifiedMobileNumberAsync(sent.MobileNumber, cancellationToken).ConfigureAwait(false);
                    return access is { MaySignIn: true } && holders.Count == 1 && holders[0] == sent.UserId
                        ? await ContinueSignInAsync(attempt, access, verifiedByIdentityProvider: null).ConfigureAwait(false)
                        : await FailAsync(attempt, AuthenticationFailureReason.AccountInactive, sent.UserId).ConfigureAwait(false);
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SmsCodeSending> SendSmsSignInCodeAsync(string mobileNumber, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> holders = await users.FindByVerifiedMobileNumberAsync(mobileNumber, cancellationToken).ConfigureAwait(false);
        if (holders.Count != 1
            || await users.FindByIdAsync(holders[0], cancellationToken).ConfigureAwait(false) is not { MaySignIn: true } access)
        {
            LogSmsSignInCodeNotSent(logger, holders.Count);
            return SmsCodeSending.Unavailable;
        }

        return await smsCodes.SendAsync(access.UserId, mobileNumber, SmsCodePurpose.SignIn, access.PreferredLanguage, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SMS sign-in code not sent: {Holders} people may sign in with that verified number (one is needed).")]
    private static partial void LogSmsSignInCodeNotSent(ILogger logger, int holders);
}
