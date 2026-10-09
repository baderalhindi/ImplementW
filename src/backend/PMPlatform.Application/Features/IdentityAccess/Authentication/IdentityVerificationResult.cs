using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// The reference of a verification Nafath completed, or why there is none. The reference is all the platform keeps of it
/// (OQ-007, nafath-data-minimisation.md): no identity attribute Nafath returned is passed on.
/// </summary>
public sealed record IdentityVerificationResult(string? Reference, AuthenticationFailure? Failure)
{
    public static IdentityVerificationResult Verified(string reference) => new(reference, null);

    public static IdentityVerificationResult Failed(AuthenticationFailure failure) => new(null, failure);
}
