using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Progress.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-02 CURRENT/LIVE Overall Project Health (TASK-044, ICD-03), as WF-02 last computed it. Read-only; the official value
/// is the latest published snapshot's.
/// </summary>
[Route("api/v1/project-health-statuses")]
[Tags("Progress")]
public sealed class ProjectHealthStatusesController(IProgressService progress) : AdministrationControllerBase
{
    /// <summary>The project's live health: one item once computed. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProgressView)]
    [ProducesResponseType<ProjectHealthStatusPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Progress_ListProjectHealthStatuses")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await progress.ListHealthStatusesAsync(CallerId, projectId!.Value, paging, cancellationToken));
}
