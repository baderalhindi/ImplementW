using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.ManagementConcern;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-07 (TASK-057): a project's issues and challenges — SCR-083 to SCR-086 — and a concern's way along its state machine. Each command
/// is one edge (R-4) and answers with the concern. Severity is the server's, computed from the impacts and pinned to the version whose
/// rule computed it; priority is people's. Validation of a resolution is WF-11's decision, on the approval endpoints.
/// </summary>
[Route(Collection)]
[Tags("ManagementConcern")]
public sealed class ManagementConcernsController(IManagementConcernService concerns, IConcernLifecycleService lifecycle) : AdministrationControllerBase
{
    private const string Collection = "api/v1/management-concerns";

    /// <summary>
    /// The project's issues and challenges, most recently changed first. <c>projectId</c> is required (R-3). Filters: <c>concernType</c>
    /// and <c>status</c> (sets), <c>assigneeUserId</c>.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConcernView)]
    [ProducesResponseType<ConcernPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_ListManagementConcerns")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? concernType, [FromQuery] string? status, [FromQuery] Guid? assigneeUserId, [FromQuery] int? page,
        [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<ConcernType> types = QueryParameters.EnumSet<ConcernType>(concernType, "concernType", errors);
        IReadOnlyCollection<ConcernStatus> statuses = QueryParameters.EnumSet<ConcernStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await concerns.ListAsync(CallerId, new ConcernQuery(projectId!.Value, types, statuses, assigneeUserId), paging, cancellationToken));
    }

    [HttpGet("{concernId:guid}")]
    [RequirePermission(PermissionCatalogue.ConcernView)]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_GetManagementConcern")]
    public async Task<IActionResult> Get(Guid concernId, CancellationToken cancellationToken) =>
        Respond(await concerns.GetAsync(CallerId, concernId, cancellationToken));

    /// <summary>Raises an issue (MOD-036) or a challenge (MOD-038), OPEN. Entities raise on their own project (ADR-013).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.ConcernRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("ManagementConcern_RaiseManagementConcern")]
    public async Task<IActionResult> Raise(ConcernCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ConcernDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await concerns.RaiseAsync(CallerId, draft!, cancellationToken), $"/{Collection}", c => c.Id);
    }

    /// <summary>The concern's own fields, as a whole (MOD-037). Requires <c>If-Match</c> (R-21). Internal users only.</summary>
    [HttpPut("{concernId:guid}")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_UpdateManagementConcern")]
    public async Task<IActionResult> Update(Guid concernId, ConcernRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ConcernChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await concerns.UpdateAsync(CallerId, concernId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>Replaces the concern's impacts; the server recomputes its severity under the scale in force and pins that version.</summary>
    [HttpPost("{concernId:guid}/assess")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_AssessManagementConcern")]
    public async Task<IActionResult> Assess(Guid concernId, ConcernAssessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out IReadOnlyList<ConcernImpactInput> impacts) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.AssessAsync(CallerId, concernId, impacts, version, ct), cancellationToken);
    }

    /// <summary>OPEN → ASSIGNED; reassigns an ASSIGNED or IN_PROGRESS concern.</summary>
    [HttpPost("{concernId:guid}/assign")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_AssignManagementConcern")]
    public async Task<IActionResult> Assign(Guid concernId, ConcernAssignCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.AssignAsync(CallerId, concernId, command.AssigneeUserId!.Value, version, ct), cancellationToken);
    }

    /// <summary>ASSIGNED → IN_PROGRESS.</summary>
    [HttpPost("{concernId:guid}/start")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_StartManagementConcern")]
    public Task<IActionResult> Start(Guid concernId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.StartAsync(CallerId, concernId, version, ct), cancellationToken);

    /// <summary>IN_PROGRESS → PENDING_VALIDATION, with the resolution; its validation is routed through WF-11.</summary>
    [HttpPost("{concernId:guid}/submit-resolution")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_SubmitManagementConcernResolution")]
    public async Task<IActionResult> SubmitResolution(Guid concernId, ConcernResolutionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out NarrativeText? resolution) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.SubmitResolutionAsync(CallerId, concernId, resolution!, version, ct), cancellationToken);
    }

    /// <summary>Records a review; the next one is due at the cadence of the project's governance profile (ADR-015).</summary>
    [HttpPost("{concernId:guid}/review")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_ReviewManagementConcern")]
    public Task<IActionResult> Review(Guid concernId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.ReviewAsync(CallerId, concernId, version, ct), cancellationToken);

    /// <summary>RESOLVED → CLOSED, once no escalation of it is OPEN. RETAIN has no DELETE (R-5).</summary>
    [HttpPost("{concernId:guid}/close")]
    [RequirePermission(PermissionCatalogue.ConcernManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConcernDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("ManagementConcern_CloseManagementConcern")]
    public Task<IActionResult> Close(Guid concernId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.CloseAsync(CallerId, concernId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ConcernDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
