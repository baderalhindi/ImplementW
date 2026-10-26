namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// The person has signed in, and passed MFA if they require it, but is an external user whose identity Nafath has not
/// verified yet (TASK-068, ADR-007, ADR-013): no session until it does. <see cref="VerificationToken"/> is accepted only by
/// the identity verification endpoints: it is not an access, refresh or MFA token. It stays valid until
/// <see cref="ExpiresAt"/> whatever happens at Nafath, so a verification that Nafath could not complete can be retried
/// without signing in again.
/// </summary>
public sealed record IdentityVerificationPending(string VerificationToken, DateTimeOffset ExpiresAt);
