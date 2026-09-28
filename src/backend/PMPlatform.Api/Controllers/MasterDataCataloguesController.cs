using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Models.MasterDataConfig;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>ADM-020–029 catalogues (TASK-034). The platform ships them (db/seed); only a catalogue's name is edited.</summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class MasterDataCataloguesController(IMasterDataAdministrationService masterData) : AdministrationControllerBase
{
    private const string Collection = "api/v1/master-data-catalogues";

    /// <summary>Unpaged: the catalogues are a small closed set (R-28). By code.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<IReadOnlyList<MasterDataCatalogueDetail>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ListMasterDataCatalogues")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await masterData.ListCataloguesAsync(cancellationToken));

    [HttpGet("{catalogueId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataView)]
    [ProducesResponseType<MasterDataCatalogueDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetMasterDataCatalogue")]
    public async Task<IActionResult> Get(Guid catalogueId, CancellationToken cancellationToken) =>
        Respond(await masterData.GetCatalogueAsync(catalogueId, cancellationToken));

    /// <summary>Requires <c>If-Match</c> (R-21).</summary>
    [HttpPut("{catalogueId:guid}")]
    [RequirePermission(PermissionCatalogue.MasterDataManage)]
    [SensitiveWrite]
    [ProducesResponseType<MasterDataCatalogueDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_UpdateMasterDataCatalogue")]
    public async Task<IActionResult> Update(Guid catalogueId, MasterDataCatalogueUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return !TryReadIfMatch(required: true, out uint? version, out IActionResult? problem) ? problem!
            : request.Validate(out BilingualLabel? name) is { Count: > 0 } errors ? ValidationFailed(errors)
            : Respond(await masterData.RenameCatalogueAsync(CallerId, catalogueId, name!, version!.Value, cancellationToken));
    }
}
