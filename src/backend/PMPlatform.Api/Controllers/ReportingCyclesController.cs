using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-02 reporting periods (TASK-044), generated from the governance profile's cadence when an update starts.</summary>
[Route("api/v1/reporting-cycles")]
[Tags("Progress")]
public sealed class ReportingCyclesController(IProgressService progress) : AdministrationControllerBase
{
    /// <summary>The project's periods, earliest first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProgressView)]
    [ProducesResponseType<ReportingCyclePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_ListReportingCycles")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await progress.ListCyclesAsync(CallerId, projectId!.Value, paging, cancellationToken));
}
