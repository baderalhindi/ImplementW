using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-04 resolution (TASK-034): the configuration of a family the platform applies to a transaction on a date, exactly as
/// a module resolves it (E-U2). A resolution is identified by the family's code.
/// </summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class ConfigurationResolutionsController(IConfigurationResolver resolver, TimeProvider timeProvider) : AdministrationControllerBase
{
    private const string Collection = "api/v1/configuration-resolutions";

    /// <summary>
    /// The version effective at <c>asOf</c> (now when absent) with its content. None, or more than one, is 422
    /// <c>CONFIGURATION_MISSING</c>: resolution fails closed.
    /// </summary>
    [HttpGet("{familyCode}")]
    [RequirePermission(PermissionCatalogue.ConfigurationView)]
    [ProducesResponseType<ResolvedConfiguration>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetConfigurationResolution")]
    public async Task<IActionResult> Get(string familyCode, [FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken) =>
        Ok(await resolver.ResolveAsync(familyCode, asOf ?? timeProvider.GetUtcNow(), cancellationToken));
}
