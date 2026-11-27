using Microsoft.Extensions.Logging;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// SR-10, AUTH-T13: the server side of a session. A sign-in records the session; every refresh or step-up must present the
/// session's current refresh token and replaces it (rotation). A refresh token presented after it was replaced was copied,
/// and since the platform cannot tell the person's copy from the thief's, the session ends for both (reuse detection).
/// Signing out ends the session, so its refresh token stops working at once.
/// </summary>
internal sealed partial class SessionRegistry(IUserSessionRepository sessions, ILogger<SessionRegistry> logger)
{
    /// <summary>Null if <paramref name="continuation"/> may continue its session; otherwise why not. A replaced token ends the session.</summary>
    public async Task<AuthenticationFailureReason?> CheckAsync(SessionContinuation continuation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return await sessions.StandingAsync(continuation.SessionId, continuation.UserId, continuation.RefreshTokenId, cancellationToken).ConfigureAwait(false) switch
        {
            RefreshTokenStanding.Current => null,
            RefreshTokenStanding.Replaced => await EndReusedAsync(continuation, cancellationToken).ConfigureAwait(false),
            RefreshTokenStanding.Ended or _ => AuthenticationFailureReason.SessionEnded,
        };
    }

    /// <summary>
    /// Records a newly issued token pair: a new session, or the continued session's next refresh token. Null once
    /// recorded; otherwise the session may not continue and the pair must not be handed out.
    /// </summary>
    public async Task<AuthenticationFailureReason?> RecordAsync(Guid userId, SessionTokens issued, SessionContinuation? continuation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(issued);
        if (continuation is null)
        {
            await sessions.StartAsync(issued.SessionId, userId, issued.RefreshTokenId, issued.SessionExpiresAt, cancellationToken).ConfigureAwait(false);
            return null;
        }

        if (await sessions.RotateAsync(continuation.SessionId, continuation.RefreshTokenId, issued.RefreshTokenId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        // Another request rotated the same token first, or the session ended meanwhile.
        return await CheckAsync(continuation, cancellationToken).ConfigureAwait(false) ?? AuthenticationFailureReason.SessionEnded;
    }

    /// <summary>True if this call ended the session; false if it had already ended (signed out, revoked or expired).</summary>
    public Task<bool> SignOutAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken) =>
        sessions.EndAsync(sessionId, UserSessionStatus.SignedOut, UserSessionEndReason.SignedOut, userId, cancellationToken);

    private async Task<AuthenticationFailureReason> EndReusedAsync(SessionContinuation continuation, CancellationToken cancellationToken)
    {
        LogRefreshTokenReused(logger, continuation.UserId, continuation.SessionId);
        await sessions.EndAsync(
                continuation.SessionId, UserSessionStatus.Revoked, UserSessionEndReason.RefreshTokenReused, continuation.UserId, cancellationToken)
            .ConfigureAwait(false);
        return AuthenticationFailureReason.RefreshTokenReused;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A replaced refresh token of user {UserId} was presented again; session {SessionId} is revoked.")]
    private static partial void LogRefreshTokenReused(ILogger logger, Guid userId, Guid sessionId);
}
