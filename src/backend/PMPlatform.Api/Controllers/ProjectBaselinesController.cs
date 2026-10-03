using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Api.Models.Schedule;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Schedule.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-03 (TASK-046): a project's baselines — SCR-061. A candidate is submitted to WF-11, or activated at once where the
/// governance profile requires no approval (ADR-015); a project has one ACTIVE baseline at a time, and activating another
/// supersedes it atomically. A baseline's review history is <c>GET /approval-instances?subjectModule=Schedule&amp;subjectType=ProjectBaseline&amp;subjectId={id}</c>.
/// </summary>
[Route(Collection)]
[Tags("Schedule")]
public sealed class ProjectBaselinesController(IBaselineService baselines) : AdministrationControllerBase
{
    private const string Collection = "api/v1/project-baselines";

    /// <summary>The project's baselines, latest version first. <c>projectId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ProjectBaselinePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListProjectBaselines")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await baselines.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{baselineId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<ProjectBaselineDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_GetProjectBaseline")]
    public async Task<IActionResult> Get(Guid baselineId, CancellationToken cancellationToken) =>
        Respond(await baselines.GetAsync(CallerId, baselineId, cancellationToken));

    /// <summary>Opens the project's next baseline version as a DRAFT candidate; one candidate at a time.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectBaselineDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Schedule_CreateProjectBaseline")]
    public async Task<IActionResult> Create(ScheduleProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await baselines.CreateAsync(CallerId, request.ProjectId!.Value, cancellationToken), $"/{Collection}", b => b.Id);
    }

    /// <summary>DRAFT or RETURNED → SUBMITTED to WF-11, freezing the plan until the outcome; or → ACTIVE where no approval is required.</summary>
    [HttpPost("{baselineId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectBaselineDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_SubmitProjectBaseline")]
    public async Task<IActionResult> Submit(Guid baselineId, BaselineSubmitCommand? command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await baselines.SubmitAsync(CallerId, baselineId, (command ?? new BaselineSubmitCommand(null)).ToSubmission(), version, cancellationToken))
            : problem!;

    /// <summary>HARD_DRAFT. R-40: a baseline that is not there — deleted already, or never the caller's to see — is 204 as well.</summary>
    [HttpDelete("{baselineId:guid}")]
    [RequirePermission(PermissionCatalogue.ScheduleEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Schedule_DeleteProjectBaseline")]
    public async Task<IActionResult> Delete(Guid baselineId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await baselines.DeleteAsync(CallerId, baselineId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>The activities the baseline froze as it activated.</summary>
    [HttpGet("{baselineId:guid}/baseline-activities")]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<BaselineActivityPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListBaselineActivities")]
    public async Task<IActionResult> ListActivities(Guid baselineId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await baselines.ListActivitiesAsync(CallerId, baselineId, paging, cancellationToken));
    }

    /// <summary>The dependencies the baseline froze as it activated.</summary>
    [HttpGet("{baselineId:guid}/baseline-dependencies")]
    [RequirePermission(PermissionCatalogue.ScheduleView)]
    [ProducesResponseType<BaselineDependencyPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Schedule_ListBaselineDependencies")]
    public async Task<IActionResult> ListDependencies(Guid baselineId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await baselines.ListDependenciesAsync(CallerId, baselineId, paging, cancellationToken));
    }
}
