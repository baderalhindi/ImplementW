using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// ADM-041, SSO/Directory Integration (TASK-028): the directory and SSO settings in effect and a connection test of
/// each. R01 only. Secret values are never returned, only whether each is set.
/// </summary>
[ApiController]
[Route("api/v1/identity-integration")]
[Tags("IdentityAccess")]
[Authorize(Policy = AuthorizationPolicies.SystemAdministrator)]
public sealed class IdentityIntegrationController(IIdentityIntegrationService integration) : ControllerBase
{
    [HttpGet]
    [EndpointName("IdentityAccess_GetIdentityIntegration")]
    public ActionResult<IdentityIntegrationStatus> Get() => integration.GetStatus();

    /// <summary>
    /// Tests both connections with the configured values. Changes nothing and stores nothing, so it takes no
    /// Idempotency-Key (R-35: no business effect to replay).
    /// </summary>
    [HttpPost("test")]
    [EndpointName("IdentityAccess_TestIdentityIntegration")]
    public async Task<ActionResult<IdentityIntegrationTestResult>> Test(CancellationToken cancellationToken) =>
        await integration.TestConnectionsAsync(cancellationToken);
}
