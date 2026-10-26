using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.Schedule;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-03 (TASK-046): the work breakdown of a project's schedule — SCR-044, SCR-045 Gantt, SCR-060 Schedule Manager. Every
/// change recalculates the schedule; each activity carries its planned dates, its Current Forecast and the ACTIVE
/// baseline's dates with the variance from them.
/// </summary>
[Route(Collection)]
[Tags("Schedule")]
public sealed class ScheduleActivitiesController(IScheduleService schedules) : AdministrationControllerBase
{
    private const string Collection = "api/v1/schedule-activities";

    /// <summary>The project's activities in sort order, then WBS code. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ScheduleActivityPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListScheduleActivities")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await schedules.ListActivitiesAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{activityId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ScheduleActivityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_GetScheduleActivity")]
    public async Task<IActionResult> Get(Guid activityId, CancellationToken cancellationToken) =>
        Respond(await schedules.GetActivityAsync(CallerId, activityId, cancellationToken));

    /// <summary>Adds an activity; its parent, if any, becomes a summary.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ScheduleActivityDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Schedule_CreateScheduleActivity")]
    public async Task<IActionResult> Create(ScheduleActivityCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ScheduleActivityDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await schedules.CreateActivityAsync(CallerId, draft!, cancellationToken), $"/{Collection}", a => a.Id);
    }

    /// <summary>The activity's inputs, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{activityId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ScheduleActivityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_UpdateScheduleActivity")]
    public async Task<IActionResult> Update(Guid activityId, ScheduleActivityRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ScheduleActivityChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await schedules.UpdateActivityAsync(CallerId, activityId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>PLANNED → CANCELLED: RETAIN has no DELETE (R-5); a cancelled activity leaves the plan and the forecast.</summary>
    [HttpPost("{activityId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ScheduleActivityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_CancelScheduleActivity")]
    public async Task<IActionResult> Cancel(Guid activityId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await schedules.CancelActivityAsync(CallerId, activityId, version, cancellationToken))
            : problem!;

    /// <summary>Sets a leaf's Current Forecast once the project has an ACTIVE baseline; the baseline never moves (BR-SCH-004).</summary>
    [HttpPost("{activityId:guid}/reforecast")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ScheduleActivityDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ReforecastScheduleActivity")]
    public async Task<IActionResult> Reforecast(Guid activityId, ScheduleForecastRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ScheduleForecast? forecast) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await schedules.ReforecastActivityAsync(CallerId, activityId, forecast!, version, cancellationToken));
    }
}
