using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Insights;
using PMPlatform.Domain.Project;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// قائمة المتابعة, RPMO's Follow-up list (the 2 Nov 2026 review, R3-5): every project the caller may see that needs following up — stalled,
/// suspended, late, with an overdue action or an open obstacle — most urgent first.
/// </summary>
[Route("api/v1/project-follow-ups")]
[Tags("Project")]
public sealed class ProjectFollowUpsController(IProjectFollowUpService followUps) : AdministrationControllerBase
{
    /// <summary>
    /// Filters: <c>departmentId</c>, <c>reason</c> (one follow-up reason), <c>followUpOwnerUserId</c>; and, from a dashboard count (review round 8,
    /// point 01), <c>mainReason</c> (the first reason only, as the dashboard counts) and the dashboard's filter bar.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectFollowUpPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectFollowUps")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? departmentId, [FromQuery] string? reason, [FromQuery] Guid? followUpOwnerUserId, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken,
        [FromQuery] string? mainReason = null, [FromQuery] Guid? externalEntityId = null, [FromQuery] Guid? classificationItemId = null,
        [FromQuery] Guid? regionItemId = null, [FromQuery] Guid? cityItemId = null, [FromQuery] string? status = null, [FromQuery] Guid? priorityItemId = null,
        [FromQuery] Guid? strategicPortfolioItemId = null, [FromQuery] Guid? strategicInitiativeItemId = null, [FromQuery] int? period = null)
    {
        List<FieldError> errors = [];
        FollowUpReason? oneReason = QueryParameters.OneEnum<FollowUpReason>(reason, "reason", errors);
        FollowUpReason? main = QueryParameters.OneEnum<FollowUpReason>(mainReason, "mainReason", errors);
        ProjectLifecycleState? lifecycle = QueryParameters.OneEnum<ProjectLifecycleState>(status, "status", errors);
        if (period is < 1900 or > 2200)
        {
            errors.Add(new FieldError("period", FieldError.OutOfRange));
        }

        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0
            ? ValidationFailed(errors)
            : Ok(await followUps.ListAsync(
                CallerId,
                new ProjectFollowUpQuery(departmentId, oneReason, followUpOwnerUserId, paging)
                {
                    MainReason = main,
                    Filters = new PortfolioInsightFilters(
                        ExternalEntityId: externalEntityId, ClassificationItemId: classificationItemId, RegionItemId: regionItemId, CityItemId: cityItemId, Status: lifecycle,
                        PriorityItemId: priorityItemId, StrategicPortfolioItemId: strategicPortfolioItemId, StrategicInitiativeItemId: strategicInitiativeItemId, Period: period),
                },
                cancellationToken));
    }
}
