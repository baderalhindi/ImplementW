using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Risk;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-06 (TASK-055): a risk's treatment and mitigation actions (MOD-034), PLANNED → IN_PROGRESS → COMPLETED, or CANCELLED.</summary>
[Route(Collection)]
[Tags("Risk")]
public sealed class RiskTreatmentActionsController(IRiskTreatmentService actions) : AdministrationControllerBase
{
    private const string Collection = "api/v1/risk-treatment-actions";

    /// <summary>The risk's actions, oldest first. <c>riskId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskTreatmentActionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_ListRiskTreatmentActions")]
    public async Task<IActionResult> List([FromQuery] Guid? riskId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(riskId, "riskId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await actions.ListActionsAsync(CallerId, riskId!.Value, paging, cancellationToken));
    }

    [HttpGet("{actionId:guid}")]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_GetRiskTreatmentAction")]
    public async Task<IActionResult> Get(Guid actionId, CancellationToken cancellationToken) =>
        Respond(await actions.GetActionAsync(CallerId, actionId, cancellationToken));

    /// <summary>Adds an action to an open risk, PLANNED.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("Risk_CreateRiskTreatmentAction")]
    public async Task<IActionResult> Create(RiskTreatmentActionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out RiskTreatmentActionDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await actions.CreateActionAsync(CallerId, draft!, cancellationToken), $"/{Collection}", a => a.Id);
    }

    /// <summary>The open action's plan, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{actionId:guid}")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_UpdateRiskTreatmentAction")]
    public async Task<IActionResult> Update(Guid actionId, RiskTreatmentActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out RiskTreatmentActionChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await actions.UpdateActionAsync(CallerId, actionId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>PLANNED → IN_PROGRESS.</summary>
    [HttpPost("{actionId:guid}/start")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_StartRiskTreatmentAction")]
    public Task<IActionResult> Start(Guid actionId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => actions.StartActionAsync(CallerId, actionId, version, ct), cancellationToken);

    /// <summary>IN_PROGRESS → COMPLETED.</summary>
    [HttpPost("{actionId:guid}/complete")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_CompleteRiskTreatmentAction")]
    public Task<IActionResult> Complete(Guid actionId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => actions.CompleteActionAsync(CallerId, actionId, version, ct), cancellationToken);

    /// <summary>PLANNED or IN_PROGRESS → CANCELLED, final: RETAIN has no DELETE (R-5).</summary>
    [HttpPost("{actionId:guid}/cancel")]
    [RequirePermission(PermissionCatalogue.RiskManage)]
    [SensitiveWrite]
    [ProducesResponseType<RiskTreatmentActionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_CancelRiskTreatmentAction")]
    public Task<IActionResult> Cancel(Guid actionId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => actions.CancelActionAsync(CallerId, actionId, version, ct), cancellationToken);

    /// <summary>A command with the caller's version when they send one (R-21).</summary>
    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<RiskTreatmentActionDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
