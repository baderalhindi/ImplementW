namespace PMPlatform.Api.Models.IdentityAccess;

/// <summary>Where to send the browser, and the sealed transaction to keep (in session storage) until the redirect back.</summary>
public sealed record SsoAuthorizationDetail(Uri AuthorizationUrl, string Transaction);
