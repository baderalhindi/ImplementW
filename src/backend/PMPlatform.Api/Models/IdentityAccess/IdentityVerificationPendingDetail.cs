namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// Signed in, but no session yet: the person is an external user whose identity Nafath has not verified (TASK-068). The
/// client starts the verification with <c>identityVerificationToken</c> (<c>POST /api/v1/sessions/identity-verification-authorization</c>)
/// and completes it (<c>POST /api/v1/sessions/identity-verification</c>). The token opens nothing else, and stays valid
/// until <c>identityVerificationTokenExpiresAt</c> so a verification Nafath could not complete can be retried.
/// </summary>
public sealed record IdentityVerificationPendingDetail(string IdentityVerificationToken, DateTimeOffset IdentityVerificationTokenExpiresAt);
