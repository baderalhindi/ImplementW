using System.Security.Claims;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>What a validated access token's claims say about its session.</summary>
internal static class SessionPrincipal
{
    public static IEnumerable<string> Roles(ClaimsPrincipal user) => user.FindAll(SessionTokenClaims.Role).Select(c => c.Value);

    /// <summary>How and when the user authenticated; null if the token does not say (and so proves no MFA and no freshness).</summary>
    public static SessionAuthentication? Authentication(ClaimsPrincipal user) =>
        SessionTokenClaims.ReadAuthentication(
            user.FindAll(SessionTokenClaims.AuthenticationMethod).Select(c => c.Value),
            user.FindFirstValue(SessionTokenClaims.AuthenticatedAt));
}
