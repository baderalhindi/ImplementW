using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-006–008 (TASK-031): the canonical roles R01–R08. They are shipped and undeletable (ERD F-080, CTL-09), so there is
/// created roles beside the eight (the 5 Nov 2026 review §6.2); a canonical role is never deleted.
/// </summary>
[Route("api/v1/roles")]
[Tags("IdentityAccess")]
public sealed class RolesController(IRoleAdministrationService roles) : AdministrationControllerBase
{
    /// <summary>The 5 Nov 2026 review §6.2: a new role and its draft profile, empty or copied from another role.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreateRole")]
    public async Task<IActionResult> Create(RoleCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out RoleCreateDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await roles.CreateRoleAsync(CallerId, draft!, cancellationToken), "/api/v1/roles", role => role.Id);
    }

    /// <summary>The 5 Nov 2026 review §6.2: the role's allowed scopes and landing dashboard. Requires <c>If-Match</c>.</summary>
    [HttpPost("{roleId:guid}/update-settings")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdateRoleSettings")]
    public async Task<IActionResult> UpdateSettings(Guid roleId, RoleSettingsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate() is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await roles.UpdateSettingsAsync(CallerId, roleId, request.AllowedScopes!, request.LandingDashboardCode, request.ShowCreateInHeader, version!.Value, cancellationToken));
    }

    /// <summary>Retires a created role: offered for no new assignment. Requires <c>If-Match</c>.</summary>
    [HttpPost("{roleId:guid}/deactivate")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_DeactivateRole")]
    public async Task<IActionResult> Deactivate(Guid roleId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: true, out uint? version, out IActionResult? problem)
            ? Respond(await roles.SetActiveAsync(CallerId, roleId, active: false, version!.Value, cancellationToken))
            : problem!;

    /// <summary>Brings a retired role back. Requires <c>If-Match</c>.</summary>
    [HttpPost("{roleId:guid}/reactivate")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<RoleDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ReactivateRole")]
    public async Task<IActionResult> Reactivate(Guid roleId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: true, out uint? version, out IActionResult? problem)
            ? Respond(await roles.SetActiveAsync(CallerId, roleId, active: true, version!.Value, cancellationToken))
            : problem!;

    /// <summary>Deletes a created role nobody ever held; one with assignments is deactivated instead. Requires <c>If-Match</c>.</summary>
    [HttpDelete("{roleId:guid}")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [EndpointName("IdentityAccess_DeleteRole")]
    public async Task<IActionResult> Delete(Guid roleId, CancellationToken cancellationToken)
    {
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : await roles.DeleteRoleAsync(CallerId, roleId, version!.Value, cancellationToken) is { } error ? Respond((AdministrationResult<Versioned<RoleDetail>>)error)
            : NoContent();
    }

    /// <summary>Unpaged (R-28): the roles, the eight canonical ones and any created.</summary>
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
