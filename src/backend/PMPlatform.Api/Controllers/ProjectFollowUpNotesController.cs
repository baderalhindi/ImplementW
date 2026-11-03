using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>A project's dated follow-up notes, newest first (the 2 Nov 2026 review, R3-3). A note is added with the follow-up command.</summary>
[Route("api/v1/project-follow-up-notes")]
[Tags("Project")]
public sealed class ProjectFollowUpNotesController(IProjectFollowUpService followUps) : AdministrationControllerBase
{
    /// <summary>The notes of <c>projectId</c>, which is required.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ProjectFollowUpNotePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListProjectFollowUpNotes")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        RequestValidation.RequireId(projectId, "projectId", errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await followUps.ListNotesAsync(CallerId, projectId!.Value, paging, cancellationToken));
    }
}
