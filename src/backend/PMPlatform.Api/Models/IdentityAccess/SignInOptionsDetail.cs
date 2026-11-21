namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>
/// The sign-in methods this environment offers. Password sign-in is not listed: the sign-in page always offers it. A passkey
/// also needs a device that has one; the page asks the browser for that.
/// </summary>
public sealed record SignInOptionsDetail(bool SingleSignOnAvailable, bool PasswordResetAvailable, bool PasskeySignInAvailable, bool SmsSignInAvailable);
