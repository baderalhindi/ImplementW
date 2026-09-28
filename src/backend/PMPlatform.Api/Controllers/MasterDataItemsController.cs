using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-020–029 items (TASK-034): authored DRAFT, validated and published by two other people, retired — never deleted
/// (RETAIN), because domain rows reference them forever.
/// </summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class MasterDataItemsController(IMasterDataAdministrationService masterData) : AdministrationControllerBase
{
    private const string Collection = "api/v1/master-data-items";

    /// <summary>Filters: <c>catalogueId</c>, <c>lifecycleState</c> (a set), <c>parentItemId</c>, <c>q</c>. In display order.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<MasterDataItemPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ListMasterDataItems")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? catalogueId, [FromQuery] string? lifecycleState, [FromQuery] Guid? parentItemId, [FromQuery] string? q,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        MasterDataItemQuery query = new(
            catalogueId, QueryParameters.EnumSet<GovernedLifecycleState>(lifecycleState, "lifecycleState", errors), parentItemId, q,
            QueryParameters.Page(page, pageSize, errors));
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await masterData.ListItemsAsync(query, cancellationToken));
    }

    [HttpGet("{itemId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetMasterDataItem")]
    public async Task<IActionResult> Get(Guid itemId, CancellationToken cancellationToken) =>
        Respond(await masterData.GetItemAsync(itemId, cancellationToken));

    [HttpPost]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("MasterDataConfig_CreateMasterDataItem")]
    public async Task<IActionResult> Create(MasterDataItemCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Validate(out MasterDataItemDraft? draft) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : RespondCreated(await masterData.CreateItemAsync(CallerId, draft!, cancellationToken), $"/{Collection}", i => i.Id);
    }

    /// <summary>Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{itemId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_UpdateMasterDataItem")]
    public async Task<IActionResult> Update(Guid itemId, MasterDataItemUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out MasterDataItemChanges? changes) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await masterData.UpdateItemAsync(CallerId, itemId, changes!, version!.Value, cancellationToken));
    }

    [HttpPost("{itemId:guid}/validate")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ValidateMasterDataItem")]
    public async Task<IActionResult> Validate(Guid itemId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await masterData.ValidateItemAsync(CallerId, itemId, version, cancellationToken))
            : problem!;

    [HttpPost("{itemId:guid}/publish")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_PublishMasterDataItem")]
    public async Task<IActionResult> Publish(Guid itemId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await masterData.PublishItemAsync(CallerId, itemId, version, cancellationToken))
            : problem!;

    [HttpPost("{itemId:guid}/retire")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataItemDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_RetireMasterDataItem")]
    public async Task<IActionResult> Retire(Guid itemId, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await masterData.RetireItemAsync(CallerId, itemId, version, cancellationToken))
            : problem!;
}
