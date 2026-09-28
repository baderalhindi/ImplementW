using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// The bearer handler's 401 and 403, in the R-23 envelope. The 401 is the same whatever was wrong with the token —
/// absent, malformed, expired, signed with a rotated key — so it says nothing about why (TASK-028). A token that was
/// presented and refused is a failed authentication, audited with the reason the caller is not told (TASK-033).
/// </summary>
internal static class BearerChallenge
{
    public static JwtBearerEvents Events() => new()
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, ErrorCodes.AuthenticationRequired, "Authentication required.")
                .ConfigureAwait(false);
        },
        OnForbidden = context =>
            ApiProblem.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.PermissionDenied, "Permission denied."),
        OnTokenValidated = MultiFactorTokenValidation.OnTokenValidated,
        OnAuthenticationFailed = context => context.HttpContext.RequestServices.GetRequiredService<IAccessAudit>().AccessTokenRejectedAsync(
            // The platform validates lifetime itself (SessionTokenValidation), so an expired token fails as an invalid lifetime.
            context.Exception is SecurityTokenExpiredException or SecurityTokenInvalidLifetimeException
                ? AuthenticationFailureReason.TokenExpired
                : AuthenticationFailureReason.TokenInvalid,
            context.Request.Path),
    };
}
