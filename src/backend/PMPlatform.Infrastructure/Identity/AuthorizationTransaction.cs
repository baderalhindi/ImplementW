namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// What an OpenID Connect authorization must remember between the redirect out and the code coming back. <see cref="Binding"/>
/// ties it to the platform user whose sign-in it continues (TASK-068), so a code obtained in one person's verification
/// cannot complete another's; an SSO sign-in has no user yet and leaves it null.
/// </summary>
internal sealed record AuthorizationTransaction(string State, string Nonce, string CodeVerifier, DateTimeOffset ExpiresAt, string? Binding = null);
