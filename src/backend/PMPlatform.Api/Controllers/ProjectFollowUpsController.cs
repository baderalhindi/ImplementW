using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// قائمة المتابعة, RPMO's Follow-up list (the 2 Nov 2026 review, R3-5): every project the caller may see that needs following up — stalled,
/// suspended, late, with an overdue action or an open obstacle — most urgent first.
/// </summary>
[Route("api/v1/project-follow-ups")]
[Tags("Project")]
public sealed class ProjectFollowUpsController(IProjectFollowUpService followUps) : AdministrationControllerBase
{
    /// <summary>Filters: <c>departmentId</c>, <c>reason</c> (one follow-up reason), <c>followUpOwnerUserId</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectFollowUpPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectFollowUps")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? departmentId, [FromQuery] string? reason, [FromQuery] Guid? followUpOwnerUserId, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        IReadOnlyCollection<FollowUpReason> reasons = QueryParameters.EnumSet<FollowUpReason>(reason, "reason", errors);
        if (reasons.Count > 1)
        {
            errors.Add(new FieldError("reason", FieldError.Malformed));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await followUps.ListAsync(CallerId, new ProjectFollowUpQuery(departmentId, reasons.Count == 1 ? reasons.First() : null, followUpOwnerUserId, paging), cancellationToken));
    }
}
