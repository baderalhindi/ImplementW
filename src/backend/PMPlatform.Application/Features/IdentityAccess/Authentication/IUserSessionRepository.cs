using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The <c>identity_access.user_session</c> table (SR-10). Each write is one conditional statement, so of two requests
/// racing with the same refresh token exactly one continues the session.
/// </summary>
public interface IUserSessionRepository
{
    public Task StartAsync(Guid sessionId, Guid userId, Guid refreshTokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>Where the refresh token <paramref name="refreshTokenId"/> stands in the session <paramref name="sessionId"/> of <paramref name="userId"/>.</summary>
    public Task<RefreshTokenStanding> StandingAsync(Guid sessionId, Guid userId, Guid refreshTokenId, CancellationToken cancellationToken);

    /// <summary>Replaces the session's refresh token if it is still active and <paramref name="presentedTokenId"/> is its current one; false otherwise.</summary>
    public Task<bool> RotateAsync(Guid sessionId, Guid presentedTokenId, Guid nextTokenId, CancellationToken cancellationToken);

    /// <summary>Ends the session if it is active; an ended one keeps how it ended. True only if this call ended it.</summary>
    public Task<bool> EndAsync(Guid sessionId, UserSessionStatus status, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken);

    /// <summary>Ends every active session of the user.</summary>
    public Task EndAllOfUserAsync(Guid userId, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken);
}

public enum RefreshTokenStanding
{
    /// <summary>The session is active and this is its current refresh token.</summary>
    Current = 1,

    /// <summary>The session is active and has already replaced this refresh token.</summary>
    Replaced = 2,

    /// <summary>No such session for this user, or it has ended.</summary>
    Ended = 3,
}
