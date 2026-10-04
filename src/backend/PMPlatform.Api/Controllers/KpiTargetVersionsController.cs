using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14 KPI Performance (TASK-052): an assignment's target versions — DRAFT, submitted to WF-11, ACTIVE on approval and never
/// edited after. A new target is a new version. Review history:
/// <c>GET /approval-instances?subjectModule=FinancialKpi&amp;subjectType=KpiTargetVersion&amp;subjectId={id}</c>.
/// </summary>
[Route(Collection)]
[Tags("FinancialKpi")]
public sealed class KpiTargetVersionsController(IKpiTargetVersionService targets) : AdministrationControllerBase
{
    private const string Collection = "api/v1/kpi-target-versions";

    /// <summary>The assignment's versions, newest first; <c>isCurrent</c> marks the ACTIVE one.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiTargetVersionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListKpiTargetVersions")]
    public async Task<IActionResult> List([FromQuery] Guid? kpiAssignmentId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(kpiAssignmentId, "kpiAssignmentId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await targets.ListAsync(CallerId, kpiAssignmentId!.Value, paging, cancellationToken));
    }

    [HttpGet("{targetVersionId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiTargetVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetKpiTargetVersion")]
    public async Task<IActionResult> Get(Guid targetVersionId, CancellationToken cancellationToken) =>
        Respond(await targets.GetAsync(CallerId, targetVersionId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiTargetVersionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_CreateKpiTargetVersion")]
    public async Task<IActionResult> Create(KpiTargetVersionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out KpiTargetVersionDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await targets.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", t => t.Id);
    }

    /// <summary>A DRAFT or RETURNED version's target and thresholds, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{targetVersionId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiTargetVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateKpiTargetVersion")]
    public async Task<IActionResult> Update(Guid targetVersionId, KpiTargetVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out KpiTargetVersionChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await targets.UpdateAsync(CallerId, targetVersionId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT. R-40: a version that is not there is 204 as well.</summary>
    [HttpDelete("{targetVersionId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("FinancialKpi_DeleteKpiTargetVersion")]
    public async Task<IActionResult> Delete(Guid targetVersionId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await targets.DeleteAsync(CallerId, targetVersionId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    /// <summary>DRAFT or RETURNED → SUBMITTED to WF-11.</summary>
    [HttpPost("{targetVersionId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.KpiManage)]
    [SensitiveWrite]
    [ProducesResponseType<KpiTargetVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_SubmitKpiTargetVersion")]
    public async Task<IActionResult> Submit(Guid targetVersionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await targets.SubmitAsync(CallerId, targetVersionId, version, cancellationToken))
            : problem!;
}
