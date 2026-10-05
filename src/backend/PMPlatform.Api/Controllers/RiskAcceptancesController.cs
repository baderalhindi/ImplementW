using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>WF-06 (TASK-055): a risk's acceptances, read only — given and revoked through the risk's commands, expired by the clock.</summary>
[Route(Collection)]
[Tags("Risk")]
public sealed class RiskAcceptancesController(IRiskService risks) : AdministrationControllerBase
{
    private const string Collection = "api/v1/risk-acceptances";

    /// <summary>The risk's acceptances, newest first. <c>riskId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskAcceptancePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_ListRiskAcceptances")]
    public async Task<IActionResult> List([FromQuery] Guid? riskId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(riskId, "riskId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await risks.ListAcceptancesAsync(CallerId, riskId!.Value, paging, cancellationToken));
    }
}
