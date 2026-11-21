using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.IdentityAccess;

/// <summary>
/// A one-time code sent by SMS to a person's mobile number (ADR-004, 13 Nov 2026 review). Only its hash is kept; it is used
/// once, within its lifetime and its attempt limit, for the purpose and the number it was sent for.
/// </summary>
public sealed class SmsCode : AuditedEntity
{
    public Guid UserId { get; set; }

    public SmsCodePurpose Purpose { get; set; }

    public required string MobileNumber { get; set; }

    /// <summary>SHA-256 of the challenge id and the code, hex.</summary>
    public required string CodeHash { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public int FailedAttempts { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>What an SMS code proves when it is entered.</summary>
public enum SmsCodePurpose
{
    /// <summary>Signs the holder of a verified number in (13 Nov 2026 review).</summary>
    SignIn = 1,

    /// <summary>Confirms that a person holds the number on their profile (ADR-004).</summary>
    MobileVerification = 2,
}
