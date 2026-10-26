using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-006–008 (TASK-031): the canonical roles R01–R08. They are shipped and undeletable (ERD F-080, CTL-09), so there is
/// no create and no delete; a new composition of permissions is a permission profile (ADR-018, TASK-110).
/// </summary>
[Route("api/v1/roles")]
[Tags("IdentityAccess")]
public sealed class RolesController(IRoleAdministrationService roles) : AdministrationControllerBase
{
    /// <summary>Unpaged (R-28): the closed set of eight canonical roles.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<IReadOnlyList<RoleSummary>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ListRoles")]
    public async Task<ActionResult<IReadOnlyList<RoleSummary>>> List(CancellationToken cancellationToken) =>
        Ok(await roles.ListRolesAsync(cancellationToken));

    [HttpGet("{roleId:guid}")]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetRole")]
    public async Task<IActionResult> Get(Guid roleId, CancellationToken cancellationToken) =>
        Respond(await roles.GetRoleAsync(roleId, cancellationToken));

    /// <summary>ADM-008: the bilingual name, the one editable part of a canonical role. Requires <c>If-Match</c>.</summary>
    [HttpPut("{roleId:guid}")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdateRole")]
    public async Task<IActionResult> Update(Guid roleId, RoleUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out BilingualLabel? name) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await roles.RenameRoleAsync(CallerId, roleId, name!, version!.Value, cancellationToken));
    }
}
