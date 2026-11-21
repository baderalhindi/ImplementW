using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.Project;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>The figures of RPMO's Excel for the Portfolio dashboard and its printable cards (the 5 Nov 2026 review §3).</summary>
[Route("api/v1/portfolio-insights")]
[Tags("Project")]
public sealed class PortfolioInsightsController(IPortfolioInsightsService insights, IPortfolioDesignService design) : AdministrationControllerBase
{
    /// <summary>Each section answers only for a caller holding its permission, at that permission's scope; the others are null.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<PortfolioInsights>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetPortfolioInsights")]
    public async Task<IActionResult> Get(
        [FromQuery] Guid? departmentId, [FromQuery] Guid? externalEntityId, [FromQuery] Guid? classificationItemId, [FromQuery] Guid? regionItemId, [FromQuery] Guid? cityItemId,
        [FromQuery] ProjectLifecycleState? status, [FromQuery] Guid? priorityItemId, [FromQuery] Guid? strategicPortfolioItemId, [FromQuery] Guid? strategicInitiativeItemId, [FromQuery] int? period,
        CancellationToken cancellationToken) =>
        Ok(await insights.GetAsync(
            CallerId,
            new PortfolioInsightFilters(departmentId, externalEntityId, classificationItemId, regionItemId, cityItemId, status, priorityItemId, strategicPortfolioItemId, strategicInitiativeItemId, period),
            cancellationToken));

    /// <summary>A printable portfolio, initiative or sector card: a summary, then every project in the caller's scope.</summary>
    [HttpGet("cards")]
    [RequirePermission(Application.Common.Authorization.PermissionCatalogue.PortfolioBreakdownView)]
    [ProducesResponseType<PortfolioCard>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetPortfolioCard")]
    public async Task<IActionResult> GetCard([FromQuery] PortfolioCardDimension dimension, [FromQuery] Guid itemId, CancellationToken cancellationToken) =>
        Respond(await insights.GetCardAsync(CallerId, dimension, itemId, cancellationToken));

    /// <summary>
    /// The Portfolio dashboard's design with the caller's figures, under the same filters as the insights (the 17 Nov 2026 designer): the saved design, or the
    /// standard one while nobody has saved one (<c>isStandard</c>). A visual whose grouping the caller holds no insights permission for has <c>groups</c> null.
    /// </summary>
    [HttpGet("design")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<PortfolioDesignView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetPortfolioDesign")]
    public async Task<IActionResult> GetDesign([FromQuery] PortfolioInsightFilters filters, CancellationToken cancellationToken) =>
        Ok(await design.GetAsync(CallerId, filters, cancellationToken));

    /// <summary>
    /// The data catalogue a visual may draw, group by and filter on (the 18 Nov 2026 data fields): every project-grain field of the projection register of
    /// a type a visual calculates with, and the aggregations its type allows. A value is still read under its own projection's permission. With
    /// <c>source=RISKS</c> (the 20 Nov 2026 record sources), the risk fields instead, each read under <c>RISK_VIEW</c>.
    /// </summary>
    [HttpGet("design/fields")]
    [RequirePermission(PermissionCatalogue.DashboardDesign)]
    [ProducesResponseType<PortfolioDesignFields>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_GetPortfolioDesignFields")]
    public async Task<IActionResult> GetDesignFields([FromQuery] PortfolioRecordSource? source, CancellationToken cancellationToken) =>
        Ok(await design.GetFieldsAsync(source, cancellationToken));

    /// <summary>A design being edited, with the caller's figures, not saved.</summary>
    [HttpPost("design/preview")]
    [RequirePermission(PermissionCatalogue.DashboardDesign)]
    [ProducesResponseType<PortfolioDesignView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_PreviewPortfolioDesign")]
    public async Task<IActionResult> PreviewDesign([FromQuery] PortfolioInsightFilters filters, PortfolioDesignRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out PortfolioDesignInput? input) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await design.PreviewAsync(CallerId, input!, filters, cancellationToken));
    }

    /// <summary>Saves the design for everyone who opens the Portfolio dashboard, as a whole, with no approval: the Portfolio Manager decides it.</summary>
    [HttpPut("design")]
    [RequirePermission(PermissionCatalogue.DashboardDesign)]
    [SensitiveWrite]
    [ProducesResponseType<PortfolioDesignView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_SavePortfolioDesign")]
    public async Task<IActionResult> SaveDesign(PortfolioDesignRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out PortfolioDesignInput? input) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : Respond(await design.SaveAsync(CallerId, input!, cancellationToken));
    }

    /// <summary>Back to the standard design.</summary>
    [HttpPost("design/reset")]
    [RequirePermission(PermissionCatalogue.DashboardDesign)]
    [SensitiveWrite]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("Project_ResetPortfolioDesign")]
    public async Task<IActionResult> ResetDesign(CancellationToken cancellationToken)
    {
        await design.ResetAsync(cancellationToken);
        return NoContent();
    }
}
