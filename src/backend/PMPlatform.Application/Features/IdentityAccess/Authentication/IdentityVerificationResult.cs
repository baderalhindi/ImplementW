using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The reference of a verification Nafath completed, or why there is none. The reference is all the platform keeps of it
/// (OQ-007, nafath-data-minimisation.md): no identity attribute Nafath returned is passed on.
/// </summary>
/// <remarks>
/// <see cref="IdentityFingerprint"/> is the keyed hash of the national identity Nafath asserted, never the number itself: the platform
/// compares it with the one recorded for the user (client review, item 7; SR-53) and keeps neither.
/// </remarks>
public sealed record IdentityVerificationResult(string? Reference, AuthenticationFailure? Failure, string? IdentityFingerprint = null)
{
    public static IdentityVerificationResult Verified(string reference, string identityFingerprint) => new(reference, null, identityFingerprint);

    public static IdentityVerificationResult Failed(AuthenticationFailure failure) => new(null, failure);
}
