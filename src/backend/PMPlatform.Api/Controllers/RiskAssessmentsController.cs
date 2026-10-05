using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Risk.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-06 (TASK-055): a risk's assessment versions, read only — an assessment is recorded by <c>POST /risks/{id}/assess</c> and is
/// never changed. Each carries the RISK_MATRIX version it pinned and the rating that version gave it.
/// </summary>
[Route(Collection)]
[Tags("Risk")]
public sealed class RiskAssessmentsController(IRiskService risks) : AdministrationControllerBase
{
    private const string Collection = "api/v1/risk-assessments";

    /// <summary>The risk's assessment versions, newest first. <c>riskId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskAssessmentPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_ListRiskAssessments")]
    public async Task<IActionResult> List([FromQuery] Guid? riskId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(riskId, "riskId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await risks.ListAssessmentsAsync(CallerId, riskId!.Value, paging, cancellationToken));
    }

    [HttpGet("{assessmentId:guid}")]
    [RequirePermission(PermissionCatalogue.RiskView)]
    [ProducesResponseType<RiskAssessmentDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Risk_GetRiskAssessment")]
    public async Task<IActionResult> Get(Guid assessmentId, CancellationToken cancellationToken) =>
        Respond(await risks.GetAssessmentAsync(CallerId, assessmentId, cancellationToken));
}
