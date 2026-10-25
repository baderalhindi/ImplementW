using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// ADR-019 (OD-S13): an external entity's person — R08, or the entity Project Manager in R04 — is given the Project Dashboard and no reports, so
/// every action of a controller carrying this answers them 404, as if the route did not exist (R-47; Build Pack 06 §A item 6) — the action
/// filter for an action they reach, the gate's refusal (<see cref="BearerChallenge"/>) for one whose permission they lack. The user type is
/// the engine's, read now, never the token's. It narrows the endpoint's own declared protection and replaces none.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
internal sealed class ExternalUsersNotFoundAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        if (await IsExternalCallerAsync(context.HttpContext).ConfigureAwait(false))
        {
            context.Result = ApiProblem.Result(context.HttpContext, StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Not found.");
            return;
        }

        await next().ConfigureAwait(false);
    }

    /// <summary>Whether the request's endpoint carries this attribute and its caller is an external user.</summary>
    public static async Task<bool> AppliesAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);
        return http.GetEndpoint()?.Metadata.GetMetadata<ExternalUsersNotFoundAttribute>() is not null
               && await IsExternalCallerAsync(http).ConfigureAwait(false);
    }

    private static async Task<bool> IsExternalCallerAsync(HttpContext http) =>
        Guid.TryParse(http.User.FindFirstValue(SessionTokenClaims.Subject), out Guid callerId)
        && await http.RequestServices.GetRequiredService<IAuthorizationEngine>().GetPrincipalAsync(callerId, http.RequestAborted).ConfigureAwait(false)
            is { UserType: UserType.External };
}
