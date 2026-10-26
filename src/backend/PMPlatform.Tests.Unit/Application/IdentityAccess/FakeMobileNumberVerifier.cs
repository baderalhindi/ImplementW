using PMPlatform.Application.Features.IdentityAccess.Administration;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>A provider that accepts <see cref="Code"/> for the challenge it issued to the user and number it was sent to.</summary>
internal sealed class FakeMobileNumberVerifier : IMobileNumberVerifier
{
    public const string ChallengeId = "challenge-1";
    public const string Code = "246810";

    public bool IsConfigured { get; set; } = true;

    public bool Available { get; set; } = true;

    public (Guid UserId, string MobileNumber)? SentTo { get; private set; }

    public Task<MobileVerificationStart> StartAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken)
    {
        if (!Available)
        {
            return Task.FromResult(new MobileVerificationStart(null, null));
        }

        SentTo = (userId, mobileNumber);
        return Task.FromResult(new MobileVerificationStart(ChallengeId, DateTimeOffset.UnixEpoch));
    }

    public Task<MobileVerificationOutcome> CompleteAsync(Guid userId, string mobileNumber, string challengeId, string code, CancellationToken cancellationToken) =>
        Task.FromResult(
            !Available ? MobileVerificationOutcome.Unavailable
            : SentTo == (userId, mobileNumber) && challengeId == ChallengeId && code == Code ? MobileVerificationOutcome.Verified
            : MobileVerificationOutcome.Rejected);
}
