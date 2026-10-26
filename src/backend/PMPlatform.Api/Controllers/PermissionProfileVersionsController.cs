using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-008 (TASK-110): a permission profile version — its grid edited while DRAFT by its author, validated by a second person and
/// published by a third, neither holding the profile; a PUBLISHED version never changes. Its assignments are moved onto the newest
/// PUBLISHED version explicitly.
/// </summary>
[Route("api/v1/permission-profile-versions")]
[Tags("IdentityAccess")]
public sealed class PermissionProfileVersionsController(IPermissionProfileAuthoringService authoring) : AdministrationControllerBase
{
    /// <summary>The version with its grants and who drafted, validated and published it; the ETag is what <c>PUT</c> and the commands name.</summary>
    [HttpGet("{versionId:guid}")]
    [RequirePermission(PermissionCatalogue.RoleView)]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_GetPermissionProfileVersion")]
    public async Task<IActionResult> Get(Guid versionId, CancellationToken cancellationToken) =>
        Respond(await authoring.GetVersionAsync(versionId, cancellationToken));

    /// <summary>Replaces a DRAFT's whole grid and change summary, by its author. Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{versionId:guid}/grants")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_UpdatePermissionProfileGrants")]
    public async Task<IActionResult> UpdateGrants(Guid versionId, PermissionGrantSetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out PermissionGrantSetChange? change) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await authoring.UpdateGrantsAsync(CallerId, versionId, change!, version!.Value, cancellationToken));
    }

    [HttpPost("{versionId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_ValidatePermissionProfileVersion")]
    public async Task<IActionResult> Validate(Guid versionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await authoring.ValidateAsync(CallerId, versionId, version, cancellationToken))
            : problem!;

    /// <summary>The version becomes assignable at once. Step-up (ADR-010).</summary>
    [HttpPost("{versionId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_PublishPermissionProfileVersion")]
    public async Task<IActionResult> Publish(Guid versionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await authoring.PublishAsync(CallerId, versionId, version, cancellationToken))
            : problem!;

    /// <summary>Abandons a draft, or withdraws a PUBLISHED version no one holds. Step-up (ADR-010).</summary>
    [HttpPost("{versionId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.RoleManage)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_RetirePermissionProfileVersion")]
    public async Task<IActionResult> Retire(Guid versionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await authoring.RetireAsync(CallerId, versionId, version, cancellationToken))
            : problem!;

    /// <summary>Moves every active assignment of the profile's older versions onto this, its newest PUBLISHED version. Step-up (ADR-010).</summary>
    [HttpPost("{versionId:guid}/assignment-migrations")]
    [RequirePermission(PermissionCatalogue.RoleAssign)]
    [SensitiveWrite]
    [ProducesResponseType<PermissionProfileMigration>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IdentityAccess_MigratePermissionProfileAssignments")]
    public async Task<IActionResult> MigrateAssignments(Guid versionId, CancellationToken cancellationToken) =>
        Respond(await authoring.MigrateAssignmentsAsync(CallerId, versionId, cancellationToken));
}
