using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Tests.Unit.Application.IdentityAccess;

/// <summary>The session table in memory, with the conditions the real repository puts in its statements.</summary>
internal sealed class FakeUserSessionRepository : IUserSessionRepository
{
    public Dictionary<Guid, UserSession> Sessions { get; } = [];

    public Task StartAsync(Guid sessionId, Guid userId, Guid refreshTokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Sessions.Add(sessionId, new UserSession { Id = sessionId, UserId = userId, Status = UserSessionStatus.Active, RefreshTokenId = refreshTokenId, ExpiresAt = expiresAt });
        return Task.CompletedTask;
    }

    public Task<RefreshTokenStanding> StandingAsync(Guid sessionId, Guid userId, Guid refreshTokenId, CancellationToken cancellationToken) =>
        Task.FromResult(
            !Sessions.TryGetValue(sessionId, out UserSession? session) || session.UserId != userId || session.Status != UserSessionStatus.Active
                ? RefreshTokenStanding.Ended
                : session.RefreshTokenId == refreshTokenId ? RefreshTokenStanding.Current : RefreshTokenStanding.Replaced);

    public Task<bool> RotateAsync(Guid sessionId, Guid presentedTokenId, Guid nextTokenId, CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(sessionId, out UserSession? session) && session.Status == UserSessionStatus.Active && session.RefreshTokenId == presentedTokenId)
        {
            session.RefreshTokenId = nextTokenId;
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> EndAsync(Guid sessionId, UserSessionStatus status, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken) =>
        Task.FromResult(Sessions.TryGetValue(sessionId, out UserSession? session) && End(session, status, reason, actorId));

    public Task EndAllOfUserAsync(Guid userId, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken)
    {
        foreach (UserSession session in Sessions.Values.Where(s => s.UserId == userId))
        {
            End(session, UserSessionStatus.Revoked, reason, actorId);
        }

        return Task.CompletedTask;
    }

    private static bool End(UserSession session, UserSessionStatus status, UserSessionEndReason reason, Guid actorId)
    {
        if (session.Status != UserSessionStatus.Active)
        {
            return false;
        }

        session.Status = status;
        session.EndReason = reason;
        session.UpdatedBy = actorId;
        return true;
    }
}
