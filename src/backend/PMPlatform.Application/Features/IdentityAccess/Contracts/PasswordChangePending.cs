namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>A local account signed in with a temporary password: no session until it is changed. The token is accepted only by the change-password step.</summary>
public sealed record PasswordChangePending(string PasswordChangeToken, DateTimeOffset ExpiresAt);

/// <summary>The account a change-password token admits, and how it signed in.</summary>
public sealed record PendingPasswordChange(Guid UserId);

/// <summary>What a local account's sign-in needs of its stored credential.</summary>
public sealed record LocalCredential(Guid UserId, string Username, string PasswordHash, bool MustChangePassword, int FailedSignInCount, DateTimeOffset? LockedUntil);
