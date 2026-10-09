namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The start of a Nafath identity verification, or the reason it cannot start (TASK-068).</summary>
/// <param name="AuthorizationUrl">Where the browser goes to verify the person's identity at Nafath.</param>
/// <param name="Transaction">
/// The state, nonce and PKCE verifier, bound to the person being verified, encrypted and time-limited. The client keeps
/// it and sends it back with the code, so the verifier never travels in a URL.
/// </param>
/// <param name="Failure">Why the verification cannot start; null when it can.</param>
public sealed record IdentityVerificationAuthorization(Uri? AuthorizationUrl, string? Transaction, AuthenticationFailure? Failure)
{
    public static IdentityVerificationAuthorization Failed(AuthenticationFailure failure) => new(null, null, failure);
}
