namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Signs a person in against AHDA's directory or identity provider and issues the platform session (TASK-028,
/// ADR-007). The platform roles in the session come from the person's platform assignments, never from directory
/// groups.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Directory credentials (LDAP bind). Every rejection is the same <see cref="AuthenticationFailure.Rejected"/>.</summary>
    public Task<AuthenticationResult> SignInWithPasswordAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>Starts an SSO sign-in: the identity provider's authorization URL and the sealed transaction the client returns with the code.</summary>
    public Task<SsoAuthorizationResult> BeginSsoSignInAsync(CancellationToken cancellationToken);

    /// <summary>Completes an SSO sign-in with the authorization code and state the identity provider redirected with.</summary>
    public Task<AuthenticationResult> CompleteSsoSignInAsync(string code, string state, string transaction, CancellationToken cancellationToken);

    /// <summary>Issues a new session token pair from a refresh token, re-reading the user's status and assignments.</summary>
    public Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
