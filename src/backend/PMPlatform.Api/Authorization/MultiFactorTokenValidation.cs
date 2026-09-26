using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// TASK-029, CTL-07: an access token is accepted only if it says how its user authenticated, and, if it holds a role
/// that requires MFA, that they passed MFA. Otherwise the bearer handler treats it as no token at all: 401 on every
/// protected endpoint, an anonymous caller on the rest. Sign-in cannot mint such a token; this also covers a token minted
/// before the policy grew, or by anything but the platform's sign-in.
/// </summary>
internal static class MultiFactorTokenValidation
{
    public static Task OnTokenValidated(TokenValidatedContext context)
    {
        MultiFactorPolicy policy = context.HttpContext.RequestServices.GetRequiredService<MultiFactorPolicy>();
        SessionAuthentication? authentication = context.Principal is { } user ? SessionPrincipal.Authentication(user) : null;

        if (authentication is null)
        {
            context.Fail("The token does not state how its user authenticated.");
        }
        else if (!authentication.MultiFactor && policy.RequiresMultiFactor(SessionPrincipal.Roles(context.Principal!)))
        {
            context.Fail("The token holds a role that requires MFA and did not pass MFA.");
        }

        return Task.CompletedTask;
    }
}
