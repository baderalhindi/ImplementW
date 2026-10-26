using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.Dashboards;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-01 at runtime (TASK-069): the three dashboards of ADR-006 — <c>PORTFOLIO</c>, <c>PROJECT</c>, <c>GOVERNANCE</c> — that carry DSH-001 to
/// DSH-012 as audience and permission renderings. A role selects a dashboard and grants no data: every widget is authorised on its source's own
/// permission and scope, and carries the R-20(c) <c>projection</c> metadata — semantic state, freshness, as-of and coverage — with an
/// explicit reason whenever its value is UNKNOWN, never 0. A dashboard the caller's roles are not an audience of is 404 (R-47).
/// </summary>
[Route(Collection)]
[Tags("Dashboards")]
public sealed class DashboardsController(IDashboardService dashboards) : AdministrationControllerBase
{
    private const string Collection = "api/v1/dashboards";

    /// <summary>The role-aware Home (FG-01 §5.1): the dashboards the caller may open, the one they land on marked <c>isDefaultLanding</c>.</summary>
    [HttpGet]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<DashboardCataloguePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Dashboards_ListDashboards")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await dashboards.ListAsync(CallerId, paging, cancellationToken));
    }

    /// <summary>
    /// The dashboard's PUBLISHED version and each widget's result. <c>PROJECT</c> is bound to <c>projectId</c> (DSH-009 in SCR-040, DSH-008 for
    /// an entity's own project); the others are read over every project the caller may see, optionally of one <c>departmentId</c> among
    /// the view's <c>departmentOptions</c>.
    /// </summary>
    [HttpGet("{dashboardCode}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<DashboardView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Dashboards_GetDashboard")]
    public async Task<IActionResult> Get(string dashboardCode, [FromQuery] Guid? projectId, [FromQuery] Guid? departmentId, CancellationToken cancellationToken) =>
        DashboardRoute.Parse(dashboardCode) is { } code
            ? Respond(await dashboards.GetAsync(CallerId, code, new DashboardContext(projectId, departmentId), cancellationToken))
            : Failure(AdministrationError.NotFound);

    /// <summary>ADR-019: hide and order the optional widgets of a personalisable dashboard, as a whole. R02, R03 and R07 hold the permission.</summary>
    [HttpPost("{dashboardCode}/personalize")]
    [RequirePermission(PermissionCatalogue.LayoutPersonalize)]
    [SensitiveWrite]
    [ProducesResponseType<DashboardPersonalizationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Dashboards_PersonalizeDashboard")]
    public async Task<IActionResult> Personalize(string dashboardCode, DashboardPersonalizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return DashboardRoute.Parse(dashboardCode) is not { } code ? Failure(AdministrationError.NotFound)
            : request.Validate(out DashboardPersonalizationInput? input) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await dashboards.PersonalizeAsync(CallerId, code, input!, cancellationToken));
    }

    /// <summary>Back to the governed layout.</summary>
    [HttpPost("{dashboardCode}/reset-personalization")]
    [RequirePermission(PermissionCatalogue.LayoutPersonalize)]
    [SensitiveWrite]
    [ProducesResponseType<DashboardPersonalizationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Dashboards_ResetDashboardPersonalization")]
    public async Task<IActionResult> ResetPersonalization(string dashboardCode, CancellationToken cancellationToken) =>
        DashboardRoute.Parse(dashboardCode) is { } code
            ? Respond(await dashboards.ResetPersonalizationAsync(CallerId, code, cancellationToken))
            : Failure(AdministrationError.NotFound);
}
