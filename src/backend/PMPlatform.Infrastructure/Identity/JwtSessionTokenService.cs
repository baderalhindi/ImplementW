using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Infrastructure.Identity;

/// <summary>
/// Signs the platform's access and refresh tokens with <c>JWT_SIGNING_KEY</c> (HS256). Both are stateless: the ERD holds
/// no session table ("session state is held by the identity provider", erd.md), so a refresh re-reads the user and
/// their assignments instead, and a refresh never outlives the session's absolute expiry.
/// </summary>
internal sealed class JwtSessionTokenService(IConfiguration configuration, IOptions<SessionTokenOptions> options, TimeProvider timeProvider) : ISessionTokenService
{
    private static readonly JsonWebTokenHandler Handler = new() { MapInboundClaims = false };

    public SessionTokens Issue(SessionTokenSubject subject, SessionContinuation? continuation)
    {
        ArgumentNullException.ThrowIfNull(subject);

        SessionTokenOptions lifetimes = options.Value;
        // Whole seconds, as a JWT carries them (RFC 7519 NumericDate), so the expiries reported with a session are the
        // ones in its tokens, and a refresh reports the same session expiry as the sign-in did.
        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        Guid sessionId = continuation?.SessionId ?? Guid.NewGuid();
        DateTimeOffset sessionExpiresAt = continuation?.SessionExpiresAt ?? (now + lifetimes.SessionLifetime);
        DateTimeOffset accessExpiresAt = Earliest(now + lifetimes.AccessTokenLifetime, sessionExpiresAt);
        DateTimeOffset refreshExpiresAt = Earliest(now + lifetimes.RefreshTokenLifetime, sessionExpiresAt);
        SigningCredentials credentials = new(SessionSigningKey.Read(configuration), SecurityAlgorithms.HmacSha256);
        string method = SessionTokenClaims.MethodValue(subject.Method);

        Dictionary<string, object> accessClaims = new(StringComparer.Ordinal)
        {
            [SessionTokenClaims.Subject] = subject.UserId.ToString(),
            [SessionTokenClaims.SessionId] = sessionId.ToString(),
            [SessionTokenClaims.UserType] = subject.UserType.ToString().ToUpperInvariant(),
            [SessionTokenClaims.AuthenticationMethod] = method,
            [SessionTokenClaims.Role] = subject.RoleCodes.ToArray(),
        };

        Dictionary<string, object> refreshClaims = new(StringComparer.Ordinal)
        {
            [SessionTokenClaims.Subject] = subject.UserId.ToString(),
            [SessionTokenClaims.SessionId] = sessionId.ToString(),
            [SessionTokenClaims.AuthenticationMethod] = method,
            [SessionTokenClaims.SessionExpiresAt] = sessionExpiresAt.ToUnixTimeSeconds(),
        };

        return new SessionTokens(
            Create(SessionTokenClaims.AccessAudience, accessClaims, now, accessExpiresAt, credentials),
            accessExpiresAt,
            Create(SessionTokenClaims.RefreshAudience, refreshClaims, now, refreshExpiresAt, credentials),
            refreshExpiresAt,
            sessionExpiresAt);
    }

    public async Task<SessionContinuation?> ReadRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        TokenValidationResult result = await Handler
            .ValidateTokenAsync(refreshToken, SessionTokenValidation.Parameters(configuration, timeProvider, SessionTokenClaims.RefreshAudience))
            .ConfigureAwait(false);
        if (!result.IsValid)
        {
            return null;
        }

        ClaimsIdentity identity = result.ClaimsIdentity;
        return Guid.TryParse(identity.FindFirst(SessionTokenClaims.Subject)?.Value, out Guid userId)
               && Guid.TryParse(identity.FindFirst(SessionTokenClaims.SessionId)?.Value, out Guid sessionId)
               && SessionTokenClaims.ParseMethod(identity.FindFirst(SessionTokenClaims.AuthenticationMethod)?.Value) is { } method
               && long.TryParse(identity.FindFirst(SessionTokenClaims.SessionExpiresAt)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long sessionExpires)
            ? new SessionContinuation(userId, sessionId, method, DateTimeOffset.FromUnixTimeSeconds(sessionExpires))
            : null;
    }

    private static string Create(
        string audience, Dictionary<string, object> claims, DateTimeOffset now, DateTimeOffset expiresAt, SigningCredentials credentials) =>
        Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = SessionTokenClaims.Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
        });

    private static DateTimeOffset Earliest(DateTimeOffset first, DateTimeOffset second) => first <= second ? first : second;
}
