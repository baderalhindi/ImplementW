using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Project;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// A project's stage plan (review round 7, item 1), one per project and named by it: every stage of the PROJECT_STAGE version in force, its weight,
/// whether it applies to the project, its planned dates and its starting %. The ETag is the project's.
/// </summary>
[Route("api/v1/project-stage-plans")]
[Tags("Project")]
public sealed class ProjectStagePlansController(IProjectStagePlanService stagePlans) : AdministrationControllerBase
{
    /// <summary>The plan. A stage not planned yet reads as applying, undated.</summary>
    [HttpGet("{projectId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectStagePlan>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetProjectStagePlan")]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken) =>
        Respond(await stagePlans.GetAsync(CallerId, projectId, cancellationToken));

    /// <summary>
    /// The whole plan. While the registration is DRAFT or RETURNED, by its writer, and a stage's dates may wait for submission; once approved, by its
    /// writer or whoever records the project's progress, once freely when it has no plan and otherwise with an approved schedule change's
    /// <c>changeAuthorizationId</c>. Requires the project's <c>If-Match</c> (R-21).
    /// </summary>
    [HttpPut("{projectId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectStagePlan>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_UpdateProjectStagePlan")]
    public async Task<IActionResult> Update(Guid projectId, ProjectStagePlanRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ProjectStagePlanChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await stagePlans.ReplaceAsync(CallerId, projectId, changes!, version!.Value, cancellationToken));
    }
}

/// <summary>The project stages in force (review round 7, item 1), in stage order, as a new registration's stage plan starts.</summary>
[Route("api/v1/project-stages")]
[Tags("Project")]
public sealed class ProjectStagesController(IProjectStagePlanService stagePlans) : AdministrationControllerBase
{
    /// <summary>Each stage applying and undated; none when no PROJECT_STAGE version is published.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectStagePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectStages")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await stagePlans.ListStagesAsync(paging, cancellationToken));
    }
}
