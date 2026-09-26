using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>How a platform session token is validated: by the API for access tokens, by the refresh path for refresh tokens.</summary>
public static class SessionTokenValidation
{
    /// <summary>The parameters the API's bearer authentication uses (composition root, L-4).</summary>
    public static TokenValidationParameters AccessToken(IConfiguration configuration, TimeProvider timeProvider) =>
        Parameters(configuration, timeProvider, SessionTokenClaims.AccessAudience);

    /// <summary>
    /// The key is resolved per token, so a rotated <c>JWT_SIGNING_KEY</c> applies at once and tokens signed with the old
    /// key stop validating. No clock skew: a token is dead at its <c>exp</c>. The lifetime is checked against
    /// <paramref name="timeProvider"/>, so tests can move the clock past it.
    /// </summary>
    internal static TokenValidationParameters Parameters(IConfiguration configuration, TimeProvider timeProvider, string audience) => new()
    {
        ValidIssuer = SessionTokenClaims.Issuer,
        ValidAudience = audience,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKeyResolver = (_, _, _, _) => [SessionSigningKey.Read(configuration)],
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) =>
        {
            DateTime now = timeProvider.GetUtcNow().UtcDateTime;
            return expires is { } expiry && now < expiry && (notBefore is null || notBefore <= now);
        },
        NameClaimType = SessionTokenClaims.Subject,
        RoleClaimType = SessionTokenClaims.Role,
    };
}
