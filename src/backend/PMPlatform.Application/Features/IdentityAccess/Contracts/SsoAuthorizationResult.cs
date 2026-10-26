namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>The start of an SSO sign-in, or the reason it cannot start.</summary>
/// <param name="AuthorizationUrl">Where the browser goes to authenticate at AHDA's identity provider.</param>
/// <param name="Transaction">
/// The state, nonce and PKCE verifier, encrypted and time-limited. The client keeps it and sends it back with the code;
/// the verifier therefore never travels in a URL.
/// </param>
/// <param name="Failure">Why the sign-in cannot start; null when it can.</param>
public sealed record SsoAuthorizationResult(Uri? AuthorizationUrl, string? Transaction, AuthenticationFailure? Failure);
