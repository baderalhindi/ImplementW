using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-03 (TASK-046): the CURRENT/LIVE Schedule Health and finish variance, as WF-03 last computed them. Read-only.</summary>
[Route("api/v1/schedule-health-statuses")]
[Tags("Schedule")]
public sealed class ScheduleHealthStatusesController(IScheduleService schedules) : AdministrationControllerBase
{
    /// <summary>The project's live Schedule Health: one item once computed. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ScheduleHealthStatusPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListScheduleHealthStatuses")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await schedules.ListHealthStatusesAsync(CallerId, projectId!.Value, paging, cancellationToken));
}
