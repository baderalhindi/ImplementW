namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>A code the provider sent; <see cref="ChallengeId"/> is null if it could not send one.</summary>
public sealed record MobileVerificationStart(string? ChallengeId, DateTimeOffset? ExpiresAt);
