using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// One-time codes sent by SMS (ADR-004, 13 Nov 2026 review), for signing in and for verifying a mobile number. The platform
/// draws the six digits and keeps only a hash; the SMS sender only delivers them. A code works once, for its purpose,
/// within <see cref="SmsCodeOptions.Lifetime"/> and <see cref="SmsCodeOptions.MaxAttempts"/>, and a person is sent at most
/// one per purpose each <see cref="SmsCodeOptions.ResendCooldown"/>.
/// </summary>
internal sealed partial class SmsOneTimeCodes(
    ISmsCodeSender sms,
    ISmsCodeRepository codes,
    SmsCodeOptions options,
    TimeProvider timeProvider,
    ILogger<SmsOneTimeCodes> logger)
{
    public bool IsConfigured => sms.IsConfigured;

    public async Task<SmsCodeSending> SendAsync(Guid userId, string mobileNumber, SmsCodePurpose purpose, Language language, CancellationToken cancellationToken)
    {
        SmsCodeOptions settings = options;
        if (!sms.IsConfigured)
        {
            return SmsCodeSending.Unavailable;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        if (await codes.LatestSentAtAsync(userId, purpose, cancellationToken).ConfigureAwait(false) is { } latest && now - latest < settings.ResendCooldown)
        {
            return SmsCodeSending.TooSoon;
        }

        Guid challengeId = Guid.CreateVersion7(now);
        string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        DateTimeOffset expiresAt = now + settings.Lifetime;
        await codes.AddAsync(
                new SmsCode
                {
                    Id = challengeId,
                    UserId = userId,
                    Purpose = purpose,
                    MobileNumber = mobileNumber,
                    CodeHash = Hash(challengeId, code),
                    ExpiresAt = expiresAt,
                    CreatedAt = now,
                    CreatedBy = userId,
                    UpdatedAt = now,
                    UpdatedBy = userId,
                },
                cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await sms.SendAsync(mobileNumber, Text(code, settings.Lifetime, language), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogNotSent(logger, userId, purpose, exception.GetType().Name);
            return SmsCodeSending.Unavailable;
        }

        return SmsCodeSending.Sent(challengeId, expiresAt);
    }

    /// <summary>The person and number the code was sent to, if <paramref name="code"/> is it and it is still good for <paramref name="purpose"/>.</summary>
    public async Task<SmsCode?> VerifyAsync(Guid challengeId, SmsCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        int maxAttempts = options.MaxAttempts;
        DateTimeOffset now = timeProvider.GetUtcNow();
        SmsCode? sent = await codes.FindAsync(challengeId, cancellationToken).ConfigureAwait(false);
        if (sent is null || sent.Purpose != purpose || sent.ConsumedAt is not null || sent.ExpiresAt <= now || sent.FailedAttempts >= maxAttempts)
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(sent.CodeHash), Encoding.ASCII.GetBytes(Hash(challengeId, code.Trim()))))
        {
            await codes.RecordFailedAttemptAsync(challengeId, cancellationToken).ConfigureAwait(false);
            return null;
        }

        return await codes.TryConsumeAsync(challengeId, now, maxAttempts, cancellationToken).ConfigureAwait(false) ? sent : null;
    }

    private static string Hash(Guid challengeId, string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{challengeId:N}:{code}")));

    private static string Text(string code, TimeSpan lifetime, Language language)
    {
        int minutes = (int)Math.Ceiling(lifetime.TotalMinutes);
        return language == Language.Ar
            ? $"رمز التحقق لمنصة إدارة المشاريع: {code}. صالح لمدة {minutes} دقائق. لا تشاركه مع أحد."
            : $"PMPlatform verification code: {code}. Valid for {minutes} minutes. Do not share it with anyone.";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SMS code for user {UserId} ({Purpose}) was not sent: {Reason}.")]
    private static partial void LogNotSent(ILogger logger, Guid userId, SmsCodePurpose purpose, string reason);
}

/// <summary>The outcome of sending a code: the challenge to answer it with, or why none was sent.</summary>
internal sealed record SmsCodeSending(Guid? ChallengeId, DateTimeOffset? ExpiresAt, bool IsTooSoon)
{
    public static SmsCodeSending Unavailable { get; } = new(null, null, IsTooSoon: false);

    public static SmsCodeSending TooSoon { get; } = new(null, null, IsTooSoon: true);

    public static SmsCodeSending Sent(Guid challengeId, DateTimeOffset expiresAt) => new(challengeId, expiresAt, IsTooSoon: false);
}
