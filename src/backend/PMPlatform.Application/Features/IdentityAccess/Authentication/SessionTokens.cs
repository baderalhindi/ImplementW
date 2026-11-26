namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

/// <summary>An issued token pair. <see cref="RefreshTokenId"/> is the refresh token's <c>jti</c>, new with every pair (SR-10).</summary>
public sealed record SessionTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset SessionExpiresAt,
    Guid SessionId,
    Guid RefreshTokenId);
