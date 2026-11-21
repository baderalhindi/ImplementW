using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.IdentityAccess;

/// <summary>The <c>identity_access.sms_code</c> table (13 Nov 2026 review).</summary>
internal sealed class SmsCodeRepository(PMPlatformDbContext context, TimeProvider timeProvider) : ISmsCodeRepository
{
    public Task<DateTimeOffset?> LatestSentAtAsync(Guid userId, SmsCodePurpose purpose, CancellationToken cancellationToken) =>
        context.Set<SmsCode>().AsNoTracking().Where(c => c.UserId == userId && c.Purpose == purpose)
            .MaxAsync(c => (DateTimeOffset?)c.CreatedAt, cancellationToken);

    public async Task AddAsync(SmsCode code, CancellationToken cancellationToken)
    {
        context.Add(code);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<SmsCode?> FindAsync(Guid challengeId, CancellationToken cancellationToken) =>
        context.Set<SmsCode>().AsNoTracking().SingleOrDefaultAsync(c => c.Id == challengeId, cancellationToken);

    public async Task RecordFailedAttemptAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        await context.Set<SmsCode>().Where(c => c.Id == challengeId).ExecuteUpdateAsync(
            set => set.SetProperty(c => c.FailedAttempts, c => c.FailedAttempts + 1).SetProperty(c => c.UpdatedAt, now),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> TryConsumeAsync(Guid challengeId, DateTimeOffset now, int maxAttempts, CancellationToken cancellationToken) =>
        await context.Set<SmsCode>()
            .Where(c => c.Id == challengeId && c.ConsumedAt == null && c.ExpiresAt > now && c.FailedAttempts < maxAttempts)
            .ExecuteUpdateAsync(set => set.SetProperty(c => c.ConsumedAt, now).SetProperty(c => c.UpdatedAt, now), cancellationToken)
            .ConfigureAwait(false) == 1;
}
