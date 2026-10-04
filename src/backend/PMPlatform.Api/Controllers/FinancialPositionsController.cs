using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.FinancialKpi;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.Progress;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-14's two financial views, never merged (M-12): the PUBLISHED/OFFICIAL snapshots, as stored at publication; the
/// CURRENT/LIVE position, computed on read; and portfolio totals over either, which add only verified SAR figures and say when
/// they are partial.
/// </summary>
[Tags("FinancialKpi")]
[OmitMaskedFields]
public sealed class FinancialPositionsController(IFinancialProgressService financials) : AdministrationControllerBase
{
    /// <summary>The project's published snapshots, latest first (I-40).</summary>
    [HttpGet("api/v1/published-financial-snapshots")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<PublishedFinancialSnapshotPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListPublishedFinancialSnapshots")]
    public async Task<IActionResult> ListSnapshots([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await financials.ListSnapshotsAsync(CallerId, projectId!.Value, paging, cancellationToken));

    /// <summary>The project's CURRENT/LIVE position: one item for a project the caller may see.</summary>
    [HttpGet("api/v1/financial-positions")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialPositionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_ListFinancialPositions")]
    public async Task<IActionResult> ListPositions([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken) =>
        ProjectCollectionQuery.Validate(projectId, page, pageSize, out PageRequest paging) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Ok(await financials.ListPositionsAsync(CallerId, projectId!.Value, paging, cancellationToken));

    /// <summary>
    /// Totals over the projects named (<c>projectId</c>, repeated, 1 to 200) from their latest snapshots
    /// (<c>semanticState=PUBLISHED_OFFICIAL</c>, the default) or their live positions (<c>CURRENT_LIVE</c>). A project not counted
    /// is listed with its reason and the aggregate is partial; with none counted every total is null.
    /// </summary>
    [HttpGet("api/v1/financial-portfolio-aggregates")]
    [RequirePermission(PermissionCatalogue.FinancialView)]
    [ProducesResponseType<FinancialPortfolioAggregate>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("FinancialKpi_GetFinancialPortfolioAggregate")]
    public async Task<IActionResult> Aggregate([FromQuery] Guid[]? projectId, [FromQuery] string? semanticState, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PortfolioQuery.RequireIds(projectId, "projectId", errors);
        IReadOnlyCollection<SemanticState> states = QueryParameters.EnumSet<SemanticState>(semanticState, "semanticState", errors);
        if (states.Count > 1)
        {
            errors.Add(new FieldError("semanticState", FieldError.NotAllowed));
        }

        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await financials.AggregateAsync(CallerId, projectId!, states.SingleOrDefault(SemanticState.PublishedOfficial), cancellationToken));
    }
}
