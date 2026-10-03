using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ProjectTask.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-04 (TASK-048): each schedule activity's actual progress as rolled up from its tasks (ADR-009) — the fact WF-02 consumes.
/// Read-only: it changes only with the tasks.
/// </summary>
[Route("api/v1/activity-execution-progresses")]
[Tags("ProjectTask")]
public sealed class ActivityExecutionProgressesController(IProjectTaskService tasks) : AdministrationControllerBase
{
    /// <summary>The project's Activity Execution Progress, by schedule activity. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.TaskView)]
    [ProducesResponseType<ActivityExecutionProgressPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ProjectTask_ListActivityExecutionProgresses")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await tasks.ListActivityProgressAsync(CallerId, projectId!.Value, paging, cancellationToken));
}
