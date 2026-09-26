namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>Issues and reads the platform's own session tokens (<c>JWT_SIGNING_KEY</c>). Implemented in Infrastructure/Identity.</summary>
public interface ISessionTokenService
{
    /// <summary>A new session, or with <paramref name="continuation"/> the next token pair of an existing one.</summary>
    public SessionTokens Issue(SessionTokenSubject subject, SessionContinuation? continuation);

    /// <summary>The session a refresh token continues, or null if the token is not a valid, unexpired refresh token.</summary>
    public Task<SessionContinuation?> ReadRefreshTokenAsync(string refreshToken);
}
