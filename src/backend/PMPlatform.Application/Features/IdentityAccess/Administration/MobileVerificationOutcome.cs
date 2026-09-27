namespace PMPlatform.Application.Features.IdentityAccess.Administration;

public enum MobileVerificationOutcome
{
    Verified = 1,

    /// <summary>Wrong, expired or spent code, or a challenge issued for another user or number.</summary>
    Rejected = 2,

    /// <summary>The provider did not answer.</summary>
    Unavailable = 3,
}
