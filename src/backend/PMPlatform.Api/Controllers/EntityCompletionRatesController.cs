using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.ExternalParticipation.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>Entities' update completion rates (review round 5, items 9 and 10). See <see cref="IEntityCompletionRates"/>.</summary>
[Tags("ExternalParticipation")]
public sealed class EntityCompletionRatesController(IEntityCompletionRates rates) : AdministrationControllerBase
{
    /// <summary>One row per entity the caller's view reaches, lowest first, and the average of all entities. <c>period</c> defaults to this month.</summary>
    [HttpGet("api/v1/entity-completion-rates")]
    [RequirePermission(PermissionCatalogue.ExternalRequestView)]
    [ProducesResponseType<EntityCompletionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_ListEntityCompletionRates")]
    public async Task<IActionResult> List([FromQuery] CompletionPeriod? period, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest request = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await rates.ListAsync(CallerId, period ?? CompletionPeriod.ThisMonth, request, cancellationToken));
    }

    /// <summary>The caller's own entity beside the average of all entities; no other entity is named. 404 for someone who belongs to no entity.</summary>
    [HttpGet("api/v1/entity-completion-rates/own")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<OwnEntityCompletion>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ExternalParticipation_GetOwnEntityCompletionRate")]
    public async Task<IActionResult> GetOwn([FromQuery] CompletionPeriod? period, CancellationToken cancellationToken) =>
        await rates.FindOwnAsync(CallerId, period ?? CompletionPeriod.ThisMonth, cancellationToken) is { } own
            ? Ok(own)
            : Failure(AdministrationError.NotFound);
}
