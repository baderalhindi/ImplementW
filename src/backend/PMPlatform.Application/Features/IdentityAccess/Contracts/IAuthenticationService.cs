namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// Signs a person in against AHDA's directory or identity provider and issues the platform session (TASK-028,
/// ADR-007). The platform roles in the session come from the person's platform assignments, never from directory
/// groups. A person who requires MFA gets no session until their second factor is verified (TASK-029, CTL-07): every
/// sign-in path ends in the same gate.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Directory credentials (LDAP bind). Every rejection is the same <see cref="AuthenticationFailure.Rejected"/>.</summary>
    public Task<AuthenticationResult> SignInWithPasswordAsync(string username, string password, CancellationToken cancellationToken);

    /// <summary>Starts an SSO sign-in: the identity provider's authorization URL and the sealed transaction the client returns with the code.</summary>
    public Task<SsoAuthorizationResult> BeginSsoSignInAsync(CancellationToken cancellationToken);

    /// <summary>Completes an SSO sign-in with the authorization code and state the identity provider redirected with.</summary>
    public Task<AuthenticationResult> CompleteSsoSignInAsync(string code, string state, string transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a new session token pair from a refresh token, re-reading the user's status and assignments. Refused if the
    /// user now requires MFA and the session never passed it.
    /// </summary>
    public Task<AuthenticationResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>Starts the second factor of a sign-in: an enrolment if the person has no factor yet, else a verification.</summary>
    public Task<MultiFactorChallengeResult> BeginMultiFactorSignInAsync(string mfaToken, CancellationToken cancellationToken);

    /// <summary>Verifies the second factor and, only then, issues the session.</summary>
    public Task<AuthenticationResult> CompleteMultiFactorSignInAsync(string mfaToken, string challengeId, string code, CancellationToken cancellationToken);

    /// <summary>Starts a step-up of the session a refresh token continues (ADR-010).</summary>
    public Task<MultiFactorChallengeResult> BeginStepUpAsync(string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a second factor for an existing session and issues its next token pair with a fresh authentication time.
    /// The session id and absolute expiry are unchanged.
    /// </summary>
    public Task<AuthenticationResult> CompleteStepUpAsync(string refreshToken, string challengeId, string code, CancellationToken cancellationToken);
}
