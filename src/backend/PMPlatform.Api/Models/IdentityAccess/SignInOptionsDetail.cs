namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>The sign-in methods this environment offers. Password sign-in is not listed: the sign-in page always offers it.</summary>
public sealed record SignInOptionsDetail(bool SingleSignOnAvailable, bool PasswordResetAvailable);
