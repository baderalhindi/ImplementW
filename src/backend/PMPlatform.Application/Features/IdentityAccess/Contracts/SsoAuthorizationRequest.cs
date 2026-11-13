namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>How an SSO sign-in starts at the identity provider.</summary>
/// <param name="LoginHint">The account to offer (OIDC <c>login_hint</c>), so the person does not type it again there; null offers none.</param>
/// <param name="Reauthenticate">
/// True to make the provider authenticate the person now (OIDC <c>prompt=login</c>) instead of reusing a session it already
/// holds. The biometric sign-in sets it, so Microsoft asks for the passkey (Face ID, Touch ID) rather than signing in silently.
/// </param>
public sealed record SsoAuthorizationRequest(string? LoginHint, bool Reauthenticate)
{
    /// <summary>The plain "Sign in with Microsoft": no account offered, the provider's own session reused.</summary>
    public static SsoAuthorizationRequest Default { get; } = new(null, Reauthenticate: false);
}
