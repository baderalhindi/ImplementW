using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>
/// Whether the MFA provider accepted the code. A wrong code, an expired or used challenge and a challenge of another
/// person are all <see cref="AuthenticationFailure.Rejected"/>.
/// </summary>
public sealed record MultiFactorVerification(bool Verified, AuthenticationFailure? Failure)
{
    public static MultiFactorVerification Passed { get; } = new(true, null);

    public static MultiFactorVerification Failed(AuthenticationFailure failure) => new(false, failure);
}
