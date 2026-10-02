using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Project;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-01 Project Creation and Registration (TASK-041): SCR-025 Project Register, SCR-026 Project Detail and MOD-001. Each
/// lifecycle edge is its own command (R-4); nothing changes a project's state implicitly. Each call is decided on the
/// project's own anchors; a project outside the caller's scope is 404 (R-47).
/// </summary>
[Route(Collection)]
[Tags("Project")]
public sealed class ProjectsController(IProjectService projects) : AdministrationControllerBase
{
    private const string Collection = "api/v1/projects";
    private const int QueryTextLength = 200;

    /// <summary>
    /// SCR-025: only projects the caller may see. Filters: <c>status</c> (a set), <c>departmentId</c>, <c>externalEntityId</c>,
    /// <c>q</c> (title or Formal Project ID). Most recently changed first.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjects")]
    public async Task<IActionResult> List(
        [FromQuery] string? status, [FromQuery] Guid? departmentId, [FromQuery] Guid? externalEntityId, [FromQuery] string? q,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.Optional(q, "q", QueryTextLength, errors);
        ProjectQuery query = new(
            QueryParameters.EnumSet<ProjectLifecycleState>(status, "status", errors), departmentId, externalEntityId, q, QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await projects.ListAsync(CallerId, query, cancellationToken));
    }

    /// <summary>SCR-026.</summary>
    [HttpGet("{projectId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetProject")]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken) =>
        Respond(await projects.GetAsync(CallerId, projectId, cancellationToken));

    /// <summary>MOD-001: a DRAFT, revision 1, with no Formal Project ID. An entity user registers for their own entity only (ADR-013).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ProjectRegister)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Project_CreateProject")]
    public async Task<IActionResult> Create(ProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ProjectDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await projects.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", p => p.Id);
    }

    /// <summary>The whole registration of a DRAFT or RETURNED project. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{projectId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectRegister)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_UpdateProject")]
    public async Task<IActionResult> Update(Guid projectId, ProjectRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ProjectDraft? draft) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await projects.UpdateAsync(CallerId, projectId, draft!, version!.Value, cancellationToken));
    }

    /// <summary>
    /// HARD_DRAFT: a DRAFT, by the person who created it. R-40: a project that is not there — deleted already, or never the
    /// caller's to see — is 204 as well.
    /// </summary>
    [HttpDelete("{projectId:guid}")]
    [RequirePermission(PermissionCatalogue.ProjectRegister)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Project_DeleteProject")]
    public async Task<IActionResult> Delete(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await projects.DeleteAsync(CallerId, projectId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>DRAFT → SUBMITTED, or RETURNED → SUBMITTED as the next revision, naming the Project Manager.</summary>
    [HttpPost("{projectId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.ProjectRegister)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_SubmitProject")]
    public async Task<IActionResult> Submit(Guid projectId, ProjectSubmitCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : command.Validate(out ProjectSubmission? submission) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await projects.SubmitAsync(CallerId, projectId, submission!, version, cancellationToken));
    }

    /// <summary>SUBMITTED → DRAFT, before AHDA starts its review.</summary>
    [HttpPost("{projectId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.ProjectRegister)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_WithdrawProject")]
    public async Task<IActionResult> Withdraw(Guid projectId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await projects.WithdrawAsync(CallerId, projectId, version, cancellationToken))
            : problem!;

    /// <summary>SUBMITTED → UNDER_REVIEW, by AHDA, starting the WF-11 run on the PROJECT_REGISTRATION route.</summary>
    [HttpPost("{projectId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.ProjectReview)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_StartProjectReview")]
    public async Task<IActionResult> StartReview(Guid projectId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await projects.StartReviewAsync(CallerId, projectId, version, cancellationToken))
            : problem!;

    /// <summary>APPROVED_PLANNED → ACTIVE, by AHDA: the only way a project becomes ACTIVE.</summary>
    [HttpPost("{projectId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.ProjectActivate)]
    [SensitiveWrite]
    [ProducesResponseType<ProjectDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ActivateProject")]
    public async Task<IActionResult> Activate(Guid projectId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await projects.ActivateAsync(CallerId, projectId, version, cancellationToken))
            : problem!;
}
