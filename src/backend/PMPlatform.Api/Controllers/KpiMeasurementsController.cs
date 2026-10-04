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
/// WF-14 KPI Performance (TASK-052): an assignment's periodic measurements, each pinned when recorded to the ACTIVE target
/// version and rated against it — a later target never changes either. A value not known is <c>null</c> with its
/// <c>valueStatus</c> saying why, and its RAG is UNKNOWN or NOT_APPLICABLE: never 0, never GREEN.
/// </summary>
[Route(Collection)]
[Tags("FinancialKpi")]
[OmitMaskedFields]
public sealed class KpiMeasurementsController(IKpiMeasurementService measurements) : AdministrationControllerBase
{
    private const string Collection = "api/v1/kpi-measurements";

    /// <summary>The assignment's measurements, latest period first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiMeasurementPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListKpiMeasurements")]
    public async Task<IActionResult> List([FromQuery] Guid? kpiAssignmentId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(kpiAssignmentId, "kpiAssignmentId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await measurements.ListAsync(CallerId, kpiAssignmentId!.Value, paging, cancellationToken));
    }

    [HttpGet("{measurementId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiView)]
    [ProducesResponseType<KpiMeasurementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetKpiMeasurement")]
    public async Task<IActionResult> Get(Guid measurementId, CancellationToken cancellationToken) =>
        Respond(await measurements.GetAsync(CallerId, measurementId, cancellationToken));

    /// <summary>Records a period's measurement as a DRAFT, pinned to the ACTIVE target version (422 <c>KPI_TARGET_NOT_APPROVED</c> without one).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.KpiRecord)]
    [SensitiveWrite]
    [ProducesResponseType<KpiMeasurementDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_CreateKpiMeasurement")]
    public async Task<IActionResult> Create(KpiMeasurementCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out KpiMeasurementDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await measurements.CreateAsync(CallerId, draft!, cancellationToken), $"/{Collection}", m => m.Id);
    }

    /// <summary>A DRAFT's value, as a whole, re-rated against its pinned version. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{measurementId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiRecord)]
    [SensitiveWrite]
    [ProducesResponseType<KpiMeasurementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateKpiMeasurement")]
    public async Task<IActionResult> Update(Guid measurementId, KpiMeasurementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out KpiMeasurementChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await measurements.UpdateAsync(CallerId, measurementId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT. R-40: a measurement that is not there is 204 as well.</summary>
    [HttpDelete("{measurementId:guid}")]
    [RequirePermission(PermissionCatalogue.KpiRecord)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("FinancialKpi_DeleteKpiMeasurement")]
    public async Task<IActionResult> Delete(Guid measurementId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await measurements.DeleteAsync(CallerId, measurementId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    [HttpPost("{measurementId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.KpiRecord)]
    [SensitiveWrite]
    [ProducesResponseType<KpiMeasurementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_SubmitKpiMeasurement")]
    public async Task<IActionResult> Submit(Guid measurementId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await measurements.SubmitAsync(CallerId, measurementId, version, cancellationToken))
            : problem!;

    /// <summary>SUBMITTED → PUBLISHED, by AHDA; not by the person who recorded it.</summary>
    [HttpPost("{measurementId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.KpiReview)]
    [SensitiveWrite]
    [ProducesResponseType<KpiMeasurementDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_PublishKpiMeasurement")]
    public async Task<IActionResult> Publish(Guid measurementId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await measurements.PublishAsync(CallerId, measurementId, version, cancellationToken))
            : problem!;
}
