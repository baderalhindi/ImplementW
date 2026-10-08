using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.Closure;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-10's closure stage (TASK-063): a project's closure cases — SCR-111 to SCR-113 at the Closure stage, distinct from and after
/// completion — and a case's way along its state machine. A COMPLETED project's case is the normal path; a SUSPENDED project's is the
/// terminal path, which closes it without completion. Once activated the project is CLOSED: terminal and read-only.
/// </summary>
[Route(Collection)]
[Tags("Closure")]
public sealed class ClosureCasesController(IClosureCaseService cases) : AdministrationControllerBase
{
    private const string Collection = "api/v1/closure-cases";

    /// <summary>The project's closure cases, most recently changed first, each with its readiness. <c>projectId</c> is required (R-3); <c>status</c> filters (a set).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.CloseoutView)]
    [ProducesResponseType<ClosureCasePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_ListClosureCases")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<CloseoutCaseStatus> statuses = QueryParameters.EnumSet<CloseoutCaseStatus>(status, "status", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await cases.ListAsync(CallerId, new CloseoutCaseQuery(projectId!.Value, statuses), paging, cancellationToken));
    }

    [HttpGet("{caseId:guid}")]
    [RequirePermission(PermissionCatalogue.CloseoutView)]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_GetClosureCase")]
    public async Task<IActionResult> Get(Guid caseId, CancellationToken cancellationToken) =>
        Respond(await cases.GetAsync(CallerId, caseId, cancellationToken));

    /// <summary>
    /// Raises a closure case (SCR-112), DRAFT: on the normal path for a COMPLETED project, following its effected completion; on the terminal
    /// path for a SUSPENDED one. A second open case is refused 409.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Closure_CreateClosureCase")]
    public async Task<IActionResult> Create(ClosureCaseCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ClosureCaseDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await cases.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", c => c.Id);
    }

    /// <summary>A DRAFT or RETURNED case's own fields, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{caseId:guid}")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_UpdateClosureCase")]
    public async Task<IActionResult> Update(Guid caseId, ClosureCaseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ClosureCaseChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await cases.UpdateAsync(CallerId, caseId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>
    /// HARD_DRAFT: a DRAFT never submitted, with no readiness records or obligations (otherwise 409 CLOSURE_CASE_IN_USE: withdraw it). R-40: a
    /// case that is not there — deleted already, or never the caller's to see — is 204 as well.
    /// </summary>
    [HttpDelete("{caseId:guid}")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Closure_DeleteClosureCase")]
    public async Task<IActionResult> Delete(Guid caseId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await cases.DeleteAsync(CallerId, caseId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>Evaluates a DRAFT or RETURNED case's readiness against the source modules and records it; the answer carries the roll-up.</summary>
    [HttpPost("{caseId:guid}/evaluate-readiness")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_EvaluateClosureReadiness")]
    public Task<IActionResult> EvaluateReadiness(Guid caseId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => cases.EvaluateReadinessAsync(CallerId, caseId, version, ct), cancellationToken);

    /// <summary>Accepts a failed, waivable criterion of a DRAFT or RETURNED case as an exception, with why. Internal users only.</summary>
    [HttpPost("{caseId:guid}/waive-check")]
    [RequirePermission(PermissionCatalogue.CloseoutWaive)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_WaiveClosureCheck")]
    public async Task<IActionResult> WaiveCheck(Guid caseId, CloseoutWaiveCheckCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ReadinessWaiver? waiver) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => cases.WaiveCheckAsync(CallerId, caseId, waiver!, version, ct), cancellationToken);
    }

    /// <summary>DRAFT or RETURNED → SUBMITTED, its readiness evaluated afresh and frozen; refused when NOT_READY. A RETURNED case comes back as the next revision.</summary>
    [HttpPost("{caseId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_SubmitClosureCase")]
    public Task<IActionResult> Submit(Guid caseId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => cases.SubmitAsync(CallerId, caseId, version, ct), cancellationToken);

    /// <summary>DRAFT, SUBMITTED or RETURNED → WITHDRAWN. Under review, the requester withdraws the WF-11 run instead, which withdraws the case.</summary>
    [HttpPost("{caseId:guid}/withdraw")]
    [RequirePermission(PermissionCatalogue.CloseoutRaise)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_WithdrawClosureCase")]
    public Task<IActionResult> Withdraw(Guid caseId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => cases.WithdrawAsync(CallerId, caseId, version, ct), cancellationToken);

    /// <summary>SUBMITTED → UNDER_REVIEW: starts the WF-11 run of the CLOSURE route. Internal users only.</summary>
    [HttpPost("{caseId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.CloseoutReview)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_StartClosureReview")]
    public Task<IActionResult> StartReview(Guid caseId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => cases.StartReviewAsync(CallerId, caseId, version, ct), cancellationToken);

    /// <summary>
    /// APPROVED → EFFECTED: the readiness revalidated, then in one transaction the project CLOSED, its open suspension ended on the terminal
    /// path, and its per-project access ended (ADR-013). A refusal leaves the case APPROVED. Safe to retry. Internal users only.
    /// </summary>
    [HttpPost("{caseId:guid}/activate")]
    [RequirePermission(PermissionCatalogue.CloseoutActivate)]
    [SensitiveWrite]
    [ProducesResponseType<ClosureCaseDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_ActivateClosureCase")]
    public Task<IActionResult> Activate(Guid caseId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => cases.ActivateAsync(CallerId, caseId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<ClosureCaseDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
