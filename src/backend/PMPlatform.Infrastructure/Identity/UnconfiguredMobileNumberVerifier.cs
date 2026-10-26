using PMPlatform.Application.Features.IdentityAccess.Administration;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// The mobile verifier until the SMS provider exists (TASK-103, blocked on OQ-012). It verifies nothing, so every
/// verification answers 503 and no number is ever marked verified without its holder confirming it (ADR-004).
/// </summary>
internal sealed class UnconfiguredMobileNumberVerifier : IMobileNumberVerifier
{
    public bool IsConfigured => false;

    public Task<MobileVerificationStart> StartAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken) =>
        Task.FromResult(new MobileVerificationStart(null, null));

    public Task<MobileVerificationOutcome> CompleteAsync(Guid userId, string mobileNumber, string challengeId, string code, CancellationToken cancellationToken) =>
        Task.FromResult(MobileVerificationOutcome.Unavailable);
}
