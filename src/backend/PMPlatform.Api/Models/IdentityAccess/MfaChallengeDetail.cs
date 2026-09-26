namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// A second-factor challenge. <c>provisioningUri</c> is set for an enrolment only: what the person loads into their
/// authenticator, as the MFA provider returns it.
/// </summary>
public sealed record MfaChallengeDetail(string ChallengeId, DateTimeOffset? ExpiresAt, string? ProvisioningUri);
