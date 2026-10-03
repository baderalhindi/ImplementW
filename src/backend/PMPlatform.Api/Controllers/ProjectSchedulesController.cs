using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.Schedule;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-03 (TASK-046): a project's working schedule, one per project, and the baseline ACTIVE on it.</summary>
[Route(Collection)]
[Tags("Schedule")]
public sealed class ProjectSchedulesController(IScheduleService schedules) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-schedules";

    /// <summary>The project's schedule: one item once initialized. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ProjectSchedulePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListProjectSchedules")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await schedules.ListSchedulesAsync(CallerId, projectId!.Value, paging, cancellationToken));

    /// <summary>Initializes the project's schedule while the project is APPROVED_PLANNED or ACTIVE.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectScheduleDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Schedule_InitializeProjectSchedule")]
    public async Task<IActionResult> Initialize(ScheduleProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await schedules.InitializeAsync(CallerId, request.ProjectId!.Value, cancellationToken), $"/{Collection}", s => s.Id);
    }
}
