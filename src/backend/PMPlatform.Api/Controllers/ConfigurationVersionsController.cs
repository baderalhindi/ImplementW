using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-04 configuration versions (TASK-034): DRAFT → VALIDATED → PUBLISHED → RETIRED with author, reviewer and publisher
/// separation. A version and its content are never deleted and never change once PUBLISHED; a change is a new version.
/// </summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class ConfigurationVersionsController(IConfigurationAdministrationService configuration) : AdministrationControllerBase
{
    private const string Collection = "api/v1/configuration-versions";

    /// <summary>Filters: <c>familyId</c>, <c>lifecycleState</c> (a set). By family, newest version first.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<ConfigurationVersionPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ListConfigurationVersions")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? familyId, [FromQuery] string? lifecycleState, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        ConfigurationVersionQuery query = new(
            familyId, QueryParameters.EnumSet<GovernedLifecycleState>(lifecycleState, "lifecycleState", errors), QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await configuration.ListVersionsAsync(query, cancellationToken));
    }

    /// <summary>The version with its whole content; the ETag is what <c>PUT</c> and the commands name.</summary>
    [HttpGet("{versionId:guid}")]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetConfigurationVersion")]
    public async Task<IActionResult> Get(Guid versionId, CancellationToken cancellationToken) =>
        Respond(await configuration.GetVersionAsync(versionId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("MasterDataConfig_CreateConfigurationVersion")]
    public async Task<IActionResult> Create(ConfigurationVersionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out ConfigurationVersionDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await configuration.CreateVersionAsync(CallerId, draft!, cancellationToken), $"/{Collection}", v => v.Id);
    }

    /// <summary>
    /// Replaces a DRAFT's change summary and whole content, by its author. Requires <c>If-Match</c> (R-21): a stale copy
    /// never overwrites a newer one.
    /// </summary>
    [HttpPut("{versionId:guid}")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_UpdateConfigurationVersion")]
    public async Task<IActionResult> Update(Guid versionId, ConfigurationVersionUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out ConfigurationVersionChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await configuration.UpdateVersionAsync(CallerId, versionId, changes!, version!.Value, cancellationToken));
    }

    [HttpPost("{versionId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ValidateConfigurationVersion")]
    public async Task<IActionResult> Validate(Guid versionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await configuration.ValidateVersionAsync(CallerId, versionId, version, cancellationToken))
            : problem!;

    /// <summary>Takes effect at <c>effectiveFrom</c>, or at once when the body or the field is absent. Step-up (ADR-010).</summary>
    [HttpPost("{versionId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_PublishConfigurationVersion")]
    public async Task<IActionResult> Publish(
        Guid versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ConfigurationVersionPublishCommand? command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await configuration.PublishVersionAsync(CallerId, versionId, command?.EffectiveFrom, version, cancellationToken))
            : problem!;

    /// <summary>Abandons a draft, or withdraws a published version from now on (before it takes effect, if it has not). Step-up (ADR-010).</summary>
    [HttpPost("{versionId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.ConfigurationManage)]
    [SensitiveWrite]
    [ProducesResponseType<ConfigurationVersionDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_RetireConfigurationVersion")]
    public async Task<IActionResult> Retire(Guid versionId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await configuration.RetireVersionAsync(CallerId, versionId, version, cancellationToken))
            : problem!;
}
