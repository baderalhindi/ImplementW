namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary><c>Identity:SmsCodes</c>: the one-time codes sent by SMS for sign-in and mobile verification.</summary>
public sealed class SmsCodeOptions
{
    public const string Section = "Identity:SmsCodes";

    /// <summary>How long a code works.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Wrong codes after which the code stops working.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>The wait before the same person can be sent another code for the same purpose; it bounds SMS cost and guessing.</summary>
    public TimeSpan ResendCooldown { get; set; } = TimeSpan.FromSeconds(60);
}
