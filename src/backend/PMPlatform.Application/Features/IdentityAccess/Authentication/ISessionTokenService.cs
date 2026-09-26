using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>Issues and reads the platform's own session tokens (<c>JWT_SIGNING_KEY</c>). Implemented in Infrastructure/Identity.</summary>
public interface ISessionTokenService
{
    /// <summary>A new session, or with <paramref name="continuation"/> the next token pair of an existing one.</summary>
    public SessionTokens Issue(SessionTokenSubject subject, SessionContinuation? continuation);

    /// <summary>The session a refresh token continues, or null if the token is not a valid, unexpired refresh token.</summary>
    public Task<SessionContinuation?> ReadRefreshTokenAsync(string refreshToken);

    /// <summary>A short-lived token that proves the first factor passed and admits the person to the second factor only.</summary>
    public MultiFactorPending IssueMultiFactorToken(PendingSignIn pending, bool enrolmentRequired);

    /// <summary>The sign-in an MFA token belongs to, or null if the token is not a valid, unexpired MFA token.</summary>
    public Task<PendingSignIn?> ReadMultiFactorTokenAsync(string mfaToken);
}
