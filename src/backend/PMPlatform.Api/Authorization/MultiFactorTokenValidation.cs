using Microsoft.AspNetCore.Authentication.JwtBearer;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// TASK-029, CTL-07: an access token is accepted only if it says how its user authenticated, and, if it holds a role
/// that requires MFA, that they passed MFA. Otherwise the bearer handler treats it as no token at all: 401 on every
/// protected endpoint, an anonymous caller on the rest. Sign-in cannot mint such a token; this also covers a token minted
/// before the policy grew, or by anything but the platform's sign-in. A refused token is audited like any other (TASK-033).
/// </summary>
internal static class MultiFactorTokenValidation
{
    public static async Task OnTokenValidated(TokenValidatedContext context)
    {
        MultiFactorPolicy policy = context.HttpContext.RequestServices.GetRequiredService<MultiFactorPolicy>();
        SessionAuthentication? authentication = context.Principal is { } user ? SessionPrincipal.Authentication(user) : null;

        AuthenticationFailureReason? refusal = authentication is null ? AuthenticationFailureReason.TokenInvalid
            : !authentication.MultiFactor && policy.RequiresMultiFactor(SessionPrincipal.Roles(context.Principal!)) ? AuthenticationFailureReason.MultiFactorMissing
            : null;
        if (refusal is not { } reason)
        {
            return;
        }

        context.Fail(reason == AuthenticationFailureReason.TokenInvalid
            ? "The token does not state how its user authenticated."
            : "The token holds a role that requires MFA and did not pass MFA.");
        await context.HttpContext.RequestServices.GetRequiredService<IAccessAudit>()
            .AccessTokenRejectedAsync(reason, context.Request.Path)
            .ConfigureAwait(false);
    }
}
