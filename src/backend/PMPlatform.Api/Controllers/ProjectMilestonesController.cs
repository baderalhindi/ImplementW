using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.Schedule;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-03 (TASK-050): the milestones of a project's schedule — SCR-046 Project Milestones. Each is the single shared milestone row
/// (ICD-04): WF-03 keeps its schedule representation and dates here; its achievement, evidence and accepted Actual Achievement
/// Date are WF-05's, at <c>/milestone-achievements?projectMilestoneId={id}</c>.
/// </summary>
[Route(Collection)]
[Tags("Schedule")]
public sealed class ProjectMilestonesController(IProjectMilestoneService milestones) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-milestones";

    /// <summary>The project's milestones, earliest forecast first, with the ACTIVE baseline's date and the variance. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ProjectMilestonePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListProjectMilestones")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await milestones.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{milestoneId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ProjectMilestoneDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_GetProjectMilestone")]
    public async Task<IActionResult> Get(Guid milestoneId, CancellationToken cancellationToken) =>
        Respond(await milestones.GetAsync(CallerId, milestoneId, cancellationToken));

    /// <summary>Adds a PLANNED milestone to the project's schedule.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectMilestoneDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Schedule_CreateProjectMilestone")]
    public async Task<IActionResult> Create(ProjectMilestoneCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ProjectMilestoneDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await milestones.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", m => m.Id);
    }

    /// <summary>A PLANNED milestone's inputs, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{milestoneId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectMilestoneDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_UpdateProjectMilestone")]
    public async Task<IActionResult> Update(Guid milestoneId, ProjectMilestoneRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ProjectMilestoneChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await milestones.UpdateAsync(CallerId, milestoneId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>PLANNED → CANCELLED: RETAIN has no DELETE (R-5); no achievement of a cancelled milestone is accepted.</summary>
    [HttpPost("{milestoneId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectMilestoneDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_CancelProjectMilestone")]
    public async Task<IActionResult> Cancel(Guid milestoneId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await milestones.CancelAsync(CallerId, milestoneId, version, cancellationToken))
            : problem!;
}
