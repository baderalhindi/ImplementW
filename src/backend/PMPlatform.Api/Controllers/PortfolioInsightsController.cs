using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>The figures of RPMO's Excel for the Portfolio dashboard and its printable cards (the 5 Nov 2026 review §3).</summary>
[Route("api/v1/portfolio-insights")]
[Tags("Project")]
public sealed class PortfolioInsightsController(IPortfolioInsightsService insights) : AdministrationControllerBase
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
}
