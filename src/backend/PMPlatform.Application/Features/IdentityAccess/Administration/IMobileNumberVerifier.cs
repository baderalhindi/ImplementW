namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// The one-time-code service that proves a person holds a mobile number (ADR-004). It sends the code by SMS and holds the
/// code, its expiry and its attempt limit; a challenge is bound to the user and the number it was sent to. The SMS provider
/// is TASK-103's, blocked on OQ-012, so until it is configured <see cref="IsConfigured"/> is false and verification
/// answers 503.
/// </summary>
public interface IMobileNumberVerifier
{
    public bool IsConfigured { get; }

    public Task<MobileVerificationStart> StartAsync(Guid userId, string mobileNumber, CancellationToken cancellationToken);

    public Task<MobileVerificationOutcome> CompleteAsync(Guid userId, string mobileNumber, string challengeId, string code, CancellationToken cancellationToken);
}
