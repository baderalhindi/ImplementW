namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// A second-factor challenge the MFA provider has started, or the <see cref="Failure"/> that stopped it.
/// <see cref="ChallengeId"/> is the provider's id for the challenge, sent back with the code. <see cref="ProvisioningUri"/>
/// is set for an enrolment only: what the person loads into their authenticator (e.g. an <c>otpauth://</c> URI), as
/// the provider returns it.
/// </summary>
public sealed record MultiFactorChallengeResult(string? ChallengeId, DateTimeOffset? ExpiresAt, string? ProvisioningUri, AuthenticationFailure? Failure)
{
    public static MultiFactorChallengeResult Failed(AuthenticationFailure failure) => new(null, null, null, failure);
}
