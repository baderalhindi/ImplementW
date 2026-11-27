using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The <c>identity_access.user_session</c> table (SR-10).</summary>
internal sealed class UserSessionRepository(PMPlatformDbContext context, TimeProvider timeProvider) : IUserSessionRepository
{
    public async Task StartAsync(Guid sessionId, Guid userId, Guid refreshTokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        context.Add(new UserSession
        {
            Id = sessionId,
            UserId = userId,
            Status = UserSessionStatus.Active,
            RefreshTokenId = refreshTokenId,
            ExpiresAt = expiresAt,
            CreatedAt = now,
            CreatedBy = userId,
            UpdatedAt = now,
            UpdatedBy = userId,
        });
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RefreshTokenStanding> StandingAsync(Guid sessionId, Guid userId, Guid refreshTokenId, CancellationToken cancellationToken)
    {
        var session = await context.Set<UserSession>().AsNoTracking()
            .Where(s => s.Id == sessionId && s.UserId == userId)
            .Select(s => new { s.Status, s.RefreshTokenId })
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return session is not { Status: UserSessionStatus.Active } ? RefreshTokenStanding.Ended
            : session.RefreshTokenId == refreshTokenId ? RefreshTokenStanding.Current
            : RefreshTokenStanding.Replaced;
    }

    public async Task<bool> RotateAsync(Guid sessionId, Guid presentedTokenId, Guid nextTokenId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return await context.Set<UserSession>()
            .Where(s => s.Id == sessionId && s.Status == UserSessionStatus.Active && s.RefreshTokenId == presentedTokenId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RefreshTokenId, nextTokenId).SetProperty(s => s.UpdatedAt, now), cancellationToken)
            .ConfigureAwait(false) == 1;
    }

    public async Task<bool> EndAsync(Guid sessionId, UserSessionStatus status, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken) =>
        await EndWhereAsync(context.Set<UserSession>().Where(s => s.Id == sessionId), status, reason, actorId, cancellationToken).ConfigureAwait(false) == 1;

    public Task EndAllOfUserAsync(Guid userId, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken) =>
        EndWhereAsync(context.Set<UserSession>().Where(s => s.UserId == userId), UserSessionStatus.Revoked, reason, actorId, cancellationToken);

    private async Task<int> EndWhereAsync(
        IQueryable<UserSession> sessions, UserSessionStatus status, UserSessionEndReason reason, Guid actorId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        return await sessions.Where(s => s.Status == UserSessionStatus.Active).ExecuteUpdateAsync(
            set => set
                .SetProperty(s => s.Status, status)
                .SetProperty(s => s.EndReason, reason)
                .SetProperty(s => s.EndedAt, now)
                .SetProperty(s => s.UpdatedAt, now)
                .SetProperty(s => s.UpdatedBy, actorId),
            cancellationToken).ConfigureAwait(false);
    }
}
