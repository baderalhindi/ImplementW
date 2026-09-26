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
/// their assignments instead, and a refresh never outlives the session's absolute expiry. The MFA token (TASK-029) is a
/// third audience: it admits its holder to the second factor and is refused everywhere else.
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
        SigningCredentials credentials = Credentials();
        string[] methods = SessionTokenClaims.MethodValues(subject.Authentication);
        long authenticatedAt = subject.Authentication.AuthenticatedAt.ToUnixTimeSeconds();

        Dictionary<string, object> accessClaims = new(StringComparer.Ordinal)
        {
            [SessionTokenClaims.Subject] = subject.UserId.ToString(),
            [SessionTokenClaims.SessionId] = sessionId.ToString(),
            [SessionTokenClaims.UserType] = subject.UserType.ToString().ToUpperInvariant(),
            [SessionTokenClaims.AuthenticationMethod] = methods,
            [SessionTokenClaims.AuthenticatedAt] = authenticatedAt,
            [SessionTokenClaims.Role] = subject.RoleCodes.ToArray(),
        };

        // The refresh token carries the authentication context forward, so a refresh neither grants nor loses MFA and
        // never makes an old authentication look fresh.
        Dictionary<string, object> refreshClaims = new(StringComparer.Ordinal)
        {
            [SessionTokenClaims.Subject] = subject.UserId.ToString(),
            [SessionTokenClaims.SessionId] = sessionId.ToString(),
            [SessionTokenClaims.AuthenticationMethod] = methods,
            [SessionTokenClaims.AuthenticatedAt] = authenticatedAt,
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
        ClaimsIdentity? identity = await ValidateAsync(refreshToken, SessionTokenClaims.RefreshAudience).ConfigureAwait(false);
        return identity is not null
               && Guid.TryParse(identity.FindFirst(SessionTokenClaims.Subject)?.Value, out Guid userId)
               && Guid.TryParse(identity.FindFirst(SessionTokenClaims.SessionId)?.Value, out Guid sessionId)
               && SessionTokenClaims.ReadAuthentication(
                   identity.FindAll(SessionTokenClaims.AuthenticationMethod).Select(c => c.Value),
                   identity.FindFirst(SessionTokenClaims.AuthenticatedAt)?.Value) is { } authentication
               && long.TryParse(identity.FindFirst(SessionTokenClaims.SessionExpiresAt)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long sessionExpires)
            ? new SessionContinuation(userId, sessionId, authentication, DateTimeOffset.FromUnixTimeSeconds(sessionExpires))
            : null;
    }

    public MultiFactorPending IssueMultiFactorToken(PendingSignIn pending, bool enrolmentRequired)
    {
        ArgumentNullException.ThrowIfNull(pending);

        DateTimeOffset now = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());
        DateTimeOffset expiresAt = now + options.Value.MultiFactorTokenLifetime;
        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            [SessionTokenClaims.Subject] = pending.UserId.ToString(),
            [SessionTokenClaims.AuthenticationMethod] = SessionTokenClaims.MethodValue(pending.Method),
        };

        return new MultiFactorPending(Create(SessionTokenClaims.MultiFactorAudience, claims, now, expiresAt, Credentials()), expiresAt, enrolmentRequired);
    }

    public async Task<PendingSignIn?> ReadMultiFactorTokenAsync(string mfaToken)
    {
        ClaimsIdentity? identity = await ValidateAsync(mfaToken, SessionTokenClaims.MultiFactorAudience).ConfigureAwait(false);
        return identity is not null
               && Guid.TryParse(identity.FindFirst(SessionTokenClaims.Subject)?.Value, out Guid userId)
               && SessionTokenClaims.ParseMethod(identity.FindFirst(SessionTokenClaims.AuthenticationMethod)?.Value) is { } method
            ? new PendingSignIn(userId, method)
            : null;
    }

    /// <summary>The token's claims if it is valid, unexpired and for <paramref name="audience"/>; otherwise null.</summary>
    private async Task<ClaimsIdentity?> ValidateAsync(string token, string audience)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        TokenValidationResult result = await Handler
            .ValidateTokenAsync(token, SessionTokenValidation.Parameters(configuration, timeProvider, audience))
            .ConfigureAwait(false);
        return result.IsValid ? result.ClaimsIdentity : null;
    }

    private SigningCredentials Credentials() => new(SessionSigningKey.Read(configuration), SecurityAlgorithms.HmacSha256);

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
