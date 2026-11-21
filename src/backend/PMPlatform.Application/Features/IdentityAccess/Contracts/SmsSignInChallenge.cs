namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A code sent by SMS for signing in: the challenge to answer it with.</summary>
public sealed record SmsSignInChallenge(Guid ChallengeId, DateTimeOffset ExpiresAt);

/// <summary>Why no sign-in code was sent.</summary>
public enum SmsSignInCodeRefusal
{
    /// <summary>No person who may sign in has this National ID or Iqama number.</summary>
    NationalIdNotRegistered = 1,

    /// <summary>The mobile number given is not the one on the person's profile.</summary>
    MobileNumberMismatch = 2,

    /// <summary>A code went to this person less than the resend wait ago.</summary>
    RecentlySent = 3,
}

/// <summary>A request for a sign-in code: sent, refused, or (no challenge, no refusal) SMS unavailable.</summary>
public sealed record SmsSignInCodeRequest(SmsSignInChallenge? Challenge, SmsSignInCodeRefusal? Refusal)
{
    public static SmsSignInCodeRequest Unavailable { get; } = new(null, null);

    public static SmsSignInCodeRequest Sent(SmsSignInChallenge challenge) => new(challenge, null);

    public static SmsSignInCodeRequest Refused(SmsSignInCodeRefusal refusal) => new(null, refusal);
}
