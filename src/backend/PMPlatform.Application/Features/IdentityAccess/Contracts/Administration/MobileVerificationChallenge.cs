namespace PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

/// <summary>A code sent to the user's mobile number; <see cref="ChallengeId"/> is returned with the code to confirm it.</summary>
public sealed record MobileVerificationChallenge(string ChallengeId, DateTimeOffset? ExpiresAt);
