using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Authorization;

/// <summary>
/// Asks the authorization engine whether the token's user holds the permission now. The token's role claims are not
/// consulted: grants are read from the database on every request, so an ended assignment or a changed profile applies
/// to the next request, not the next sign-in (TASK-031). For the same reason MFA is checked against the roles held
/// now: an access token issued without MFA grants nothing once its user holds a role that requires MFA (CTL-07).
/// </summary>
internal sealed class PermissionAuthorizationHandler(IAuthorizationEngine engine, MultiFactorPolicy multiFactorPolicy)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(SessionTokenClaims.Subject), out Guid userId))
        {
            return;
        }

        CancellationToken cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        AuthorizationPrincipal? principal = await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false);
        if (principal is null
            || (multiFactorPolicy.RequiresMultiFactor(principal.RoleCodes) && SessionPrincipal.Authentication(context.User)?.MultiFactor != true))
        {
            return;
        }

        AuthorizationDecision decision = await engine.AuthorizeAsync(userId, new AuthorizationRequest(requirement.PermissionCode), cancellationToken)
            .ConfigureAwait(false);
        if (decision.IsAllowed)
        {
            context.Succeed(requirement);
        }
    }
}
