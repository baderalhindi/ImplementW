using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Project.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// What waits on each project the signed-in person manages (review round 8, point 13): the Project Manager's Home finds what needs action this
/// month — an update due, a task or milestone overdue — without reading each project.
/// </summary>
[Route("api/v1/users/current/managed-project-attention")]
[Tags("Project")]
public sealed class ManagedProjectAttentionController(IManagedProjectAttentionService attention) : AdministrationControllerBase
{
    /// <summary>Every project PROJECT_VIEW reaches for the caller that names them as manager, with the period due by month end and the overdue counts.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ProjectView)]
    [ProducesResponseType<ManagedProjectAttentionList>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("Project_ListManagedProjectAttention")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await attention.ListAsync(CallerId, cancellationToken));
}
