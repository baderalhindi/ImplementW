using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADR-004's verification over the platform's own SMS codes (13 Nov 2026 review): it works wherever an SMS gateway is
/// configured. A code verifies only the user and the number it was sent to; a code asked for again within the resend wait
/// is refused like an outage (503), and the person tries again shortly.
/// </summary>
internal sealed class SmsCodeMobileNumberVerifier(SmsOneTimeCodes codes, IUserAccessRepository users) : IMobileNumberVerifier
{
    public bool IsConfigured => codes.IsConfigured;

    public async Task<MobileVerificationStart> StartAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken)
    {
        Language language = (await users.FindByIdAsync(userId, cancellationToken).ConfigureAwait(false))?.PreferredLanguage ?? Language.Ar;
        SmsCodeSending sending = await codes.SendAsync(userId, mobileNumber, SmsCodePurpose.MobileVerification, language, cancellationToken).ConfigureAwait(false);
        return sending.ChallengeId is { } challengeId
            ? new MobileVerificationStart(challengeId.ToString(), sending.ExpiresAt)
            : new MobileVerificationStart(null, null);
    }

    public async Task<MobileVerificationOutcome> CompleteAsync(Guid userId, string mobileNumber, string challengeId, string code, CancellationToken cancellationToken) =>
        !codes.IsConfigured ? MobileVerificationOutcome.Unavailable
        : Guid.TryParse(challengeId, out Guid id)
          && await codes.VerifyAsync(id, SmsCodePurpose.MobileVerification, code, cancellationToken).ConfigureAwait(false) is { } sent
          && sent.UserId == userId
          && sent.MobileNumber == mobileNumber
            ? MobileVerificationOutcome.Verified
            : MobileVerificationOutcome.Rejected;
}
