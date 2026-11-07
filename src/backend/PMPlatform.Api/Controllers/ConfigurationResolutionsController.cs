using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-04 resolution (TASK-034): the configuration of a family the platform applies to a transaction on a date, exactly as
/// a module resolves it (E-U2). A resolution is identified by the family's code.
/// </summary>
[Route(Collection)]
[Tags("MasterDataConfig")]
public sealed class ConfigurationResolutionsController(
    IConfigurationResolver resolver, IAuthorizationEngine engine, IAuthorizationService authorization, TimeProvider timeProvider) : AdministrationControllerBase
{
    private const string Collection = "api/v1/configuration-resolutions";

    private static readonly Dictionary<string, string> GovernedReads = new(StringComparer.Ordinal)
    {
        [ConfigurationFamilyCodes.RiskMatrix] = PermissionCatalogue.RiskView,
        [ConfigurationFamilyCodes.GovernanceProfile] = PermissionCatalogue.ProjectView,
    };

    /// <summary>
    /// The version effective at <c>asOf</c> (now when absent) with its content. None, or more than one, is 422
    /// <c>CONFIGURATION_MISSING</c>: resolution fails closed. Needs CONFIGURATION_VIEW, or the permission to read what the family governs: a
    /// risk's rating cannot be read without the matrix, nor a project's level without the profiles (review round 5, items 12 and 15).
    /// </summary>
    [HttpGet("{familyCode}")]
    [AllowAnyAuthenticatedUser]
    [ProducesResponseType<ResolvedConfiguration>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("MasterDataConfig_GetConfigurationResolution")]
    public async Task<IActionResult> Get(string familyCode, [FromQuery] DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        string permission = PermissionCatalogue.ConfigurationView;
        if (GovernedReads.TryGetValue(familyCode, out string? governed)
            && await engine.GetPrincipalAsync(CallerId, cancellationToken) is { } caller
            && caller.Grants.Any(g => g.PermissionCode == governed))
        {
            permission = governed;
        }

        AuthorizationResult allowed = await authorization.AuthorizeAsync(User, null, new PermissionRequirement(permission));
        return allowed.Succeeded
            ? Ok(await resolver.ResolveAsync(familyCode, asOf ?? timeProvider.GetUtcNow(), cancellationToken))
            : Failure(AdministrationError.Forbidden);
    }
}
