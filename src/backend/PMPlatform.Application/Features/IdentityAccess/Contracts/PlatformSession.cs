namespace PMPlatform.Application.Features.IdentityAccess.Contracts;

/// <summary>
/// An issued session: a short-lived access token for <c>Authorization: Bearer</c> (api-conventions R-46), a refresh
/// token, and the user it was issued to. A refresh never extends <see cref="SessionExpiresAt"/>.
/// </summary>
public sealed record PlatformSession(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset SessionExpiresAt,
    SessionUser User);
