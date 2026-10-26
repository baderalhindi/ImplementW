using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>FG-04 configuration families (TASK-034), each with the version in force now.</summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class ConfigurationFamiliesController(IConfigurationAdministrationService configuration) : AdministrationControllerBase
{
    private const string Collection = "api/v1/configuration-families";

    /// <summary>Unpaged: the twelve families (R-28), each with the version resolution returns now.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<IReadOnlyList<ConfigurationFamilySummary>>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_ListConfigurationFamilies")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) => Ok(await configuration.ListFamiliesAsync(cancellationToken));
}
