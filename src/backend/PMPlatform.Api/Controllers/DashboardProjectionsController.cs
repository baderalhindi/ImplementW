using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Dashboards.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// The projection register (TASK-069; FG-01 §4, API-DSH-041): every source projection a widget may bind to, with its owning domain, contract
/// version, semantic state, supported contexts and widget types, and the source permission its values are authorised on. Nothing outside it
/// can be bound (BR-DSH-029).
/// </summary>
[Route("api/v1/dashboard-projections")]
[Tags("Dashboards")]
public sealed class DashboardProjectionsController(IDashboardDefinitionService definitions) : AdministrationControllerBase
{
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<DashboardProjectionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Dashboards_ListDashboardProjections")]
    public IActionResult List([FromQuery] int? page, [FromQuery] int? pageSize)
    {
        List<FieldError> errors = [];
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(definitions.ListProjections(paging));
    }
}
