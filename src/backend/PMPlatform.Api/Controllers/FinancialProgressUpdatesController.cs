using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14 Financial Progress (TASK-052): a reporting period's CURRENT/LIVE actual expenditure and forecast, one revision each,
/// reviewed and published by AHDA into an immutable Published Financial Snapshot. A figure not known is <c>null</c> with its
/// <c>valueStatus</c> saying why — never <c>"0.00"</c>. Each workflow edge is its own command (R-4).
/// </summary>
[Route(Collection)]
[Tags("FinancialKpi")]
[OmitMaskedFields]
public sealed class FinancialProgressUpdatesController(IFinancialProgressService financials) : AdministrationControllerBase
{
    private const string Collection = "api/v1/financial-progress-updates";

    /// <summary>Every revision of the project's periods, newest first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialProgressUpdatePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListFinancialProgressUpdates")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await financials.ListAsync(CallerId, projectId!.Value, paging, cancellationToken));

    [HttpGet("{updateId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetFinancialProgressUpdate")]
    public async Task<IActionResult> Get(Guid updateId, CancellationToken cancellationToken) =>
        Respond(await financials.GetAsync(CallerId, updateId, cancellationToken));

    /// <summary>Opens the earliest begun WF-02 period without published figures as a DRAFT, every figure Unknown (MISSING).</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("FinancialKpi_StartFinancialProgressUpdate")]
    public async Task<IActionResult> Start(FinancialProgressUpdateStartRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate() is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await financials.StartAsync(CallerId, request.ProjectId!.Value, cancellationToken), $"/{Collection}", u => u.Id);
    }

    /// <summary>A DRAFT's figures and provenance, as a whole. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{updateId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_UpdateFinancialProgressUpdate")]
    public async Task<IActionResult> Update(Guid updateId, FinancialProgressUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out FinancialProgressUpdateChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await financials.UpdateAsync(CallerId, updateId, changes!, version!.Value, cancellationToken));
    }

    /// <summary>HARD_DRAFT. R-40: an update that is not there is 204 as well.</summary>
    [HttpDelete("{updateId:guid}")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("FinancialKpi_DeleteFinancialProgressUpdate")]
    public async Task<IActionResult> Delete(Guid updateId, CancellationToken cancellationToken)
    {
        if (!TryReadIfMatch(required: false, out uint? version, out IActionResult? problem))
        {
            return problem!;
        }

        AdministrationError? error = await financials.DeleteAsync(CallerId, updateId, version, cancellationToken);
        return error is null || error.Kind == AdministrationErrorKind.NotFound ? NoContent() : Failure(error);
    }

    [HttpPost("{updateId:guid}/submit")]
    [RequirePermission(PermissionCatalogue.FinancialSubmit)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_SubmitFinancialProgressUpdate")]
    public async Task<IActionResult> Submit(Guid updateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await financials.SubmitAsync(CallerId, updateId, version, cancellationToken))
            : problem!;

    /// <summary>SUBMITTED → UNDER_REVIEW, by AHDA; not by the submitter.</summary>
    [HttpPost("{updateId:guid}/start-review")]
    [RequirePermission(PermissionCatalogue.FinancialReview)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_StartFinancialProgressUpdateReview")]
    public async Task<IActionResult> StartReview(Guid updateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await financials.StartReviewAsync(CallerId, updateId, version, cancellationToken))
            : problem!;

    /// <summary>UNDER_REVIEW → RETURNED with the reason; the period continues as revision + 1.</summary>
    [HttpPost("{updateId:guid}/return")]
    [RequirePermission(PermissionCatalogue.FinancialReview)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ReturnFinancialProgressUpdate")]
    public async Task<IActionResult> Return(Guid updateId, ProgressReturnCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: false, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out NarrativeText? reason) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await financials.ReturnAsync(CallerId, updateId, reason!, version, cancellationToken));
    }

    /// <summary>UNDER_REVIEW → PUBLISHED, writing the period's snapshot with the status rated under pinned thresholds.</summary>
    [HttpPost("{updateId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.FinancialReview)]
    [SensitiveWrite]
    [ProducesResponseType<FinancialProgressUpdateDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_PublishFinancialProgressUpdate")]
    public async Task<IActionResult> Publish(Guid updateId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await financials.PublishAsync(CallerId, updateId, version, cancellationToken))
            : problem!;
}
