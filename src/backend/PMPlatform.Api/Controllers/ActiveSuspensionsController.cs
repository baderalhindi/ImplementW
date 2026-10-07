using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Suspension.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-09 (TASK-062): a project's suspension periods — the one open while it is SUSPENDED, and the ended ones of earlier cycles
/// (BR-SUS-039). Read-only: a period is opened and ended only by activating a request.
/// </summary>
[Route("api/v1/active-suspensions")]
[Tags("Suspension")]
public sealed class ActiveSuspensionsController(ISuspensionRequestService requests) : AdministrationControllerBase
{
    /// <summary>The project's suspension periods, most recently started first. <c>projectId</c> is required (R-3); <c>open</c> filters open or ended ones.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.SuspensionView)]
    [ProducesResponseType<ActiveSuspensionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Suspension_ListActiveSuspensions")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] bool? open, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await requests.ListSuspensionsAsync(CallerId, new ActiveSuspensionQuery(projectId!.Value, open), paging, cancellationToken));
    }
}
