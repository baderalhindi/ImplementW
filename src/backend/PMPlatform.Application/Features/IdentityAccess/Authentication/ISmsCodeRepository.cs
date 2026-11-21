using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>The <c>identity_access.sms_code</c> table. The two writes that decide a code are single statements, so parallel guesses all count.</summary>
public interface ISmsCodeRepository
{
    public Task<DateTimeOffset?> LatestSentAtAsync(Guid userId, SmsCodePurpose purpose, CancellationToken cancellationToken);

    public Task AddAsync(SmsCode code, CancellationToken cancellationToken);

    public Task<SmsCode?> FindAsync(Guid challengeId, CancellationToken cancellationToken);

    public Task RecordFailedAttemptAsync(Guid challengeId, CancellationToken cancellationToken);

    /// <summary>Marks the code used; false if it was used, expired or locked meanwhile, so a code works once.</summary>
    public Task<bool> TryConsumeAsync(Guid challengeId, DateTimeOffset now, int maxAttempts, CancellationToken cancellationToken);
}
