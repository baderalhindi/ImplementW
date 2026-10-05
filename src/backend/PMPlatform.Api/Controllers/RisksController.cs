using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-06 (TASK-055): a project's risk register — SCR-080 Risk Register, SCR-082 Risk Detail — and a risk's way along its state
/// machine. Each command is one edge (R-4) and answers with the risk. Rating is the server's, from the RISK_MATRIX version in force
/// at assessment time, which the assessment pins; the reopen is controlled by a permission of its own.
/// </summary>
[Route(Collection)]
[Tags("Risk")]
public sealed class RisksController(IRiskService risks, IRiskLifecycleService lifecycle) : AdministrationControllerBase
{
    private const string Collection = "api/v1/risks";

    /// <summary>
    /// The project's risks, most recently changed first, each with its current assessment and rating. <c>projectId</c> is required
    /// (R-3). Filters: <c>status</c> (a set), <c>ownerUserId</c>, <c>nextReviewDateFrom</c> and <c>nextReviewDateTo</c> (both included).
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_ListRisks")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? projectId, [FromQuery] string? status, [FromQuery] Guid? ownerUserId, [FromQuery] DateOnly? nextReviewDateFrom,
        [FromQuery] DateOnly? nextReviewDateTo, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        IReadOnlyCollection<RiskStatus> statuses = QueryParameters.EnumSet<RiskStatus>(status, "status", errors);
        if (nextReviewDateTo < nextReviewDateFrom)
        {
            errors.Add(new FieldError("nextReviewDateTo", FieldError.DateBeforeStart));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await risks.ListRisksAsync(CallerId, new RiskQuery(projectId!.Value, statuses, ownerUserId, nextReviewDateFrom, nextReviewDateTo), paging, cancellationToken));
    }

    [HttpGet("{riskId:guid}")]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_GetRisk")]
    public async Task<IActionResult> Get(Guid riskId, CancellationToken cancellationToken) =>
        Respond(await risks.GetRiskAsync(CallerId, riskId, cancellationToken));

    /// <summary>Registers a risk, IDENTIFIED (MOD-030). Refused on a project whose governance profile carries no risk management (ADR-015).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Risk_RegisterRisk")]
    public async Task<IActionResult> Register(RiskCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out RiskDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await risks.RegisterRiskAsync(CallerId, draft!, cancellationToken), $"/{Collection}", r => r.Id);
    }

    /// <summary>The risk's register fields, as a whole (MOD-031, MOD-033). Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{riskId:guid}")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_UpdateRisk")]
    public async Task<IActionResult> Update(Guid riskId, RiskRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out RiskChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await risks.UpdateRiskAsync(CallerId, riskId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>
    /// Records an assessment (MOD-032), rated by the RISK_MATRIX version in force now, which it pins: publishing the matrix again
    /// never changes this rating. IDENTIFIED → ASSESSED on the first. Internal users only (ADR-013).
    /// </summary>
    [HttpPost("{riskId:guid}/assess")]
    [RequirePermission(PermissionCatalogue.RiskAssess)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_AssessRisk")]
    public async Task<IActionResult> Assess(Guid riskId, RiskAssessCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out RiskAssessmentDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.AssessAsync(CallerId, riskId, draft!, version, ct), cancellationToken);
    }

    /// <summary>ASSESSED or MONITORING → TREATMENT, with a live treatment action and no active acceptance.</summary>
    [HttpPost("{riskId:guid}/start-treatment")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_StartRiskTreatment")]
    public Task<IActionResult> StartTreatment(Guid riskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.StartTreatmentAsync(CallerId, riskId, version, ct), cancellationToken);

    /// <summary>ASSESSED or TREATMENT → MONITORING.</summary>
    [HttpPost("{riskId:guid}/monitor")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_MonitorRisk")]
    public Task<IActionResult> Monitor(Guid riskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.MonitorAsync(CallerId, riskId, version, ct), cancellationToken);

    /// <summary>
    /// Accepts an assessed risk until <c>expiresOn</c>, after today — there is no permanent acceptance — and moves it to MONITORING.
    /// On that day it returns for review. Internal users only (ADR-013).
    /// </summary>
    [HttpPost("{riskId:guid}/accept")]
    [RequirePermission(PermissionCatalogue.RiskAccept)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_AcceptRisk")]
    public async Task<IActionResult> Accept(Guid riskId, RiskAcceptCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out RiskAcceptanceDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.AcceptAsync(CallerId, riskId, draft!, version, ct), cancellationToken);
    }

    /// <summary>Ends the active acceptance before its expiry; the risk returns to ASSESSED for review. Internal users only (ADR-013).</summary>
    [HttpPost("{riskId:guid}/revoke-acceptance")]
    [RequirePermission(PermissionCatalogue.RiskAccept)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_RevokeRiskAcceptance")]
    public Task<IActionResult> RevokeAcceptance(Guid riskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.RevokeAcceptanceAsync(CallerId, riskId, version, ct), cancellationToken);

    /// <summary>An open risk → CLOSED, with its rationale (MOD-035); an active acceptance ends with it. RETAIN has no DELETE (R-5).</summary>
    [HttpPost("{riskId:guid}/close")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_CloseRisk")]
    public async Task<IActionResult> Close(Guid riskId, RiskCloseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out NarrativeText? rationale) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.CloseAsync(CallerId, riskId, rationale!, version, ct), cancellationToken);
    }

    /// <summary>CLOSED → ASSESSED, or IDENTIFIED if never assessed. RISK_REOPEN only: general edit permission never reopens a risk.</summary>
    [HttpPost("{riskId:guid}/reopen")]
    [RequirePermission(PermissionCatalogue.RiskReopen)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_ReopenRisk")]
    public Task<IActionResult> Reopen(Guid riskId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => lifecycle.ReopenAsync(CallerId, riskId, version, ct), cancellationToken);

    /// <summary>
    /// Raises an issue from an open risk through WF-07 (edge 15) and records when; the issue holds the risk as its origin. 503 until
    /// WF-07 is built (TASK-057).
    /// </summary>
    [HttpPost("{riskId:guid}/materialise")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_MaterialiseRisk")]
    public async Task<IActionResult> Materialise(Guid riskId, RiskMaterialiseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out RiskMaterialisationDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => lifecycle.MaterialiseAsync(CallerId, riskId, draft!, version, ct), cancellationToken);
    }

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<RiskDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
