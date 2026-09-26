using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMPlatform.Api.Errors;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// The bearer handler's 401 and 403, in the R-23 envelope. The 401 is the same whatever was wrong with the token —
/// absent, malformed, expired, signed with a rotated key — so it says nothing about why (TASK-028).
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
    };
}
