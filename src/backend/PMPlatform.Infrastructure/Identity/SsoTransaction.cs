namespace PMPlatform.Infrastructure.Identity;

/// <summary>What an SSO sign-in must remember between the redirect out and the code coming back.</summary>
internal sealed record SsoTransaction(string State, string Nonce, string CodeVerifier, DateTimeOffset ExpiresAt);
