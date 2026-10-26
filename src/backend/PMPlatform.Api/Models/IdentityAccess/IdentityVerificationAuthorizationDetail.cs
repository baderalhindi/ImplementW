namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Where to send the browser for Nafath, and the sealed transaction to keep (in session storage) until it comes back to <c>NAFATH_CALLBACK_URL</c>.</summary>
public sealed record IdentityVerificationAuthorizationDetail(Uri AuthorizationUrl, string Transaction);
