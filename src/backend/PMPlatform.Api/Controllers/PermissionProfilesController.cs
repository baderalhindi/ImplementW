using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-007/009 (TASK-031): the permission profiles and, per version, their grants — the permission matrix. Authoring,
/// validating and publishing a version is TASK-110's; a version's id is what ADM-010 assigns.
/// </summary>
[Route("api/v1/permission-profiles")]
[Tags("IdentityAccess")]
public sealed class PermissionProfilesController(IRoleAdministrationService roles) : IdentityAccessControllerBase
{
    /// <summary>Filter: <c>baseRoleId</c>. By code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<PermissionProfilePage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListPermissionProfiles")]
    public async Task<IActionResult> List([FromQuery] Guid? baseRoleId, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        PageRequest pageRequest = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await roles.ListProfilesAsync(baseRoleId, pageRequest, cancellationToken));
    }

    /// <summary>The profile with every version, newest first, and each version's grants.</summary>
    [HttpGet("{permissionProfileId:guid}")]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<PermissionProfileDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetPermissionProfile")]
    public async Task<IActionResult> Get(Guid permissionProfileId, CancellationToken cancellationToken) =>
        Respond(await roles.GetProfileAsync(permissionProfileId, cancellationToken));
}
