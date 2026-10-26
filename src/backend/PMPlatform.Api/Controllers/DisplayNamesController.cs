using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// Names only, for every signed-in user (the 24 Oct 2026 review, H1: business users saw ids where names belong). The records that carry
/// the ids are authorised by their own modules; an external user is told its own entity and its own entity's people alone.
/// </summary>
[Tags("IdentityAccess")]
public sealed class DisplayNamesController(IDisplayNameService names) : AdministrationControllerBase
{
    /// <summary>The display names of the people named (<c>userId</c>, repeated, up to 100), in the order asked for.</summary>
    [HttpGet("api/v1/display-names")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<DisplayNames>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListDisplayNames")]
    public async Task<IActionResult> List([FromQuery] Guid[]? userId, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        if (userId is not null && userId.Contains(Guid.Empty))
        {
            errors.Add(new FieldError("userId", FieldError.Malformed));
        }
        else if (userId is not null && userId.Length > DisplayNameQuery.MaxIds)
        {
            errors.Add(new FieldError("userId", FieldError.OutOfRange));
        }

        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await names.ListAsync(CallerId, new DisplayNameQuery(userId ?? []), cancellationToken));
    }

    /// <summary>The departments and external entities by code, with their names and whether each is active: what screens name and forms offer.</summary>
    [HttpGet("api/v1/organization-units")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<OrganizationUnits>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListOrganizationUnits")]
    public async Task<IActionResult> Units(CancellationToken cancellationToken) => Ok(await names.ListUnitsAsync(CallerId, cancellationToken));
}
