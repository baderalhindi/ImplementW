namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The answer to a request for an SMS sign-in code. It is the same whether or not the number belongs to anyone, so it says
/// nothing about who holds which number; a code was sent only if it did.
/// </summary>
public sealed record SmsSignInChallenge(Guid ChallengeId, DateTimeOffset ExpiresAt);
