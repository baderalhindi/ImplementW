using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-007/009 (TASK-031): the permission profiles and, per version, their grants — the permission matrix. ADM-008 (TASK-110):
/// a new profile, or a new DRAFT version of one; the version is then edited and governed under <c>permission-profile-versions</c>.
/// A version's id is what ADM-010 assigns.
/// </summary>
[Route(Collection)]
[Tags("IdentityAccess")]
public sealed class PermissionProfilesController(IRoleAdministrationService roles, IPermissionProfileAuthoringService authoring) : AdministrationControllerBase
{
    private const string Collection = "api/v1/permission-profiles";

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

    /// <summary>A new profile on a canonical role; its version 1 is a DRAFT, copying <c>sourceVersionId</c>'s grants when given.</summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreatePermissionProfile")]
    public async Task<IActionResult> Create(PermissionProfileCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out PermissionProfileCreateDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await authoring.CreateProfileAsync(CallerId, draft!, cancellationToken), "/api/v1/permission-profile-versions", v => v.Id);
    }

    /// <summary>The profile's next version, a DRAFT copying <c>sourceVersionId</c> or its newest PUBLISHED version. One open draft at a time.</summary>
    [HttpPost("{permissionProfileId:guid}/versions")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IdentityAccess_CreatePermissionProfileVersion")]
    public async Task<IActionResult> CreateVersion(
        Guid permissionProfileId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PermissionProfileVersionCreateRequest? request,
        CancellationToken cancellationToken) =>
        RespondCreated(await authoring.CreateVersionAsync(CallerId, permissionProfileId, request?.SourceVersionId, cancellationToken),
            "/api/v1/permission-profile-versions", v => v.Id);
}
