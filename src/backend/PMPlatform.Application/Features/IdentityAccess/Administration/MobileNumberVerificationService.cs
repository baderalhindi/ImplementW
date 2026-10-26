using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// ADR-004's verification step, taken by the user for their own number. The number is marked verified only when the
/// provider accepts the code for this user and the number stored now: a number changed since the code was sent is not
/// verified by it.
/// </summary>
internal sealed partial class MobileNumberVerificationService(
    IUserAdministrationRepository users,
    IMobileNumberVerifier verifier,
    TimeProvider timeProvider,
    ILogger<MobileNumberVerificationService> logger) : IMobileNumberVerificationService
{
    public async Task<AdministrationResult<MobileVerificationChallenge>> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        UserDetail? user = (await users.FindDetailAsync(userId, cancellationToken).ConfigureAwait(false))?.Value;
        if (Refusal(user?.Status, user?.MobileNumber, user?.MobileVerifiedAt) is { } refused)
        {
            return refused;
        }

        MobileVerificationStart start = await verifier.StartAsync(userId, user!.MobileNumber!, cancellationToken).ConfigureAwait(false);
        return start.ChallengeId is { } challengeId
            ? new MobileVerificationChallenge(challengeId, start.ExpiresAt)
            : AdministrationError.Unavailable;
    }

    public async Task<AdministrationResult<MobileVerificationResult>> CompleteAsync(Guid userId, string challengeId, string code, CancellationToken cancellationToken)
    {
        User? user = await users.FindForUpdateAsync(userId, expectedVersion: null, cancellationToken).ConfigureAwait(false);
        if (Refusal(user?.Status, user?.MobileNumber, user?.MobileVerifiedAt) is { } refused)
        {
            return refused;
        }

        string mobileNumber = user!.MobileNumber!;
        switch (await verifier.CompleteAsync(userId, mobileNumber, challengeId, code, cancellationToken).ConfigureAwait(false))
        {
            case MobileVerificationOutcome.Unavailable:
                return AdministrationError.Unavailable;
            case MobileVerificationOutcome.Rejected:
                LogRejected(logger, userId);
                return AdministrationError.Rule(IdentityAccessErrorCodes.MobileVerificationFailed);
            case MobileVerificationOutcome.Verified:
                break;
            default:
                throw new InvalidOperationException("Unknown verification outcome.");
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        user.MobileVerifiedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = userId;

        // The row was read without an expected version, so a conflict means the number was edited meanwhile: the code
        // proved the old one.
        if ((await users.SaveAsync(cancellationToken).ConfigureAwait(false)).Outcome != SaveOutcome.Saved)
        {
            return AdministrationError.Rule(IdentityAccessErrorCodes.MobileVerificationFailed);
        }

        LogVerified(logger, userId);
        return new MobileVerificationResult(mobileNumber, now);
    }

    private AdministrationError? Refusal(UserStatus? status, string? mobileNumber, DateTimeOffset? verifiedAt) =>
        status is null ? AdministrationError.NotFound
        : status != UserStatus.Active ? AdministrationError.Forbidden
        : mobileNumber is null ? AdministrationError.Rule(IdentityAccessErrorCodes.MobileNumberMissing, new FieldIssue("mobileNumber", FieldIssue.Required))
        : verifiedAt is not null ? AdministrationError.InvalidTransition
        : !verifier.IsConfigured ? AdministrationError.Unavailable
        : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Mobile number of user {UserId} verified.")]
    private static partial void LogVerified(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mobile number verification code rejected for user {UserId}.")]
    private static partial void LogRejected(ILogger logger, Guid userId);
}
