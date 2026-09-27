using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>ADM-009 (TASK-031): the protected permission catalogue (ERD F-081). Defined in code and seeded, so read-only here.</summary>
[Route("api/v1/permissions")]
[Tags("IdentityAccess")]
public sealed class PermissionsController(IRoleAdministrationService roles) : IdentityAccessControllerBase
{
    /// <summary>Unpaged (R-28): the catalogue is a small closed set, by group and code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<IReadOnlyList<PermissionSummary>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListPermissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await roles.ListPermissionsAsync(cancellationToken));
}
