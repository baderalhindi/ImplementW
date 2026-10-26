using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// WF-10 (TASK-063): a completion or closure case's readiness history — every evaluation and waiver as recorded, newest first. The
/// evaluation a submission made is that revision's frozen readiness snapshot. Read-only: records are appended by the case's commands.
/// </summary>
[Route("api/v1/readiness-checks")]
[Tags("Closure")]
public sealed class ReadinessChecksController(IReadinessRecordService records) : AdministrationControllerBase
{
    /// <summary>One case's records: exactly one of <c>completionCaseId</c> and <c>closureCaseId</c> is required (R-3).</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.CloseoutView)]
    [ProducesResponseType<ReadinessRecordPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Closure_ListReadinessChecks")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? completionCaseId, [FromQuery] Guid? closureCaseId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        if ((completionCaseId is null) == (closureCaseId is null))
        {
            errors.Add(new FieldError(completionCaseId is null ? "completionCaseId" : "closureCaseId", completionCaseId is null ? FieldError.Required : FieldError.NotAllowed));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await records.ListAsync(CallerId, new ReadinessRecordQuery(completionCaseId, closureCaseId), paging, cancellationToken));
    }
}
