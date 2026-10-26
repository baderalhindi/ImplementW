namespace PMPlatform.Application.Features.IdentityAccess.Authentication;

public sealed record SessionTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    DateTimeOffset SessionExpiresAt);
