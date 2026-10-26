using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IntegrationMonitoring;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-05 ADM-040 Integrations Overview and ADM-046 Integration Detail (TASK-075): every integration with its instances, each with availability
/// and freshness as two indicators, its measures (ADR-004: delivery rate, failure rate, latency, receipt latency), and MOD-101's activation.
/// No secret, endpoint value or payload is ever returned: an endpoint is named by its configuration key.
/// </summary>
[Route(Collection)]
[Tags("IntegrationMonitoring")]
public sealed class IntegrationsController(IIntegrationCatalogue catalogue, IHostEnvironment environment) : AdministrationControllerBase
{
    private const string Collection = "api/v1/integrations";

    /// <summary>The catalogue, by code, with the environment it describes.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<IntegrationCatalogueView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_ListIntegrations")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(new IntegrationCatalogueView(environment.EnvironmentName, await catalogue.ListAsync(cancellationToken)));

    /// <summary>One integration with its policy and each instance's measures from <c>from</c> to <c>to</c>; by default over the last metrics window.</summary>
    [HttpGet("{integrationId:guid}")]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<IntegrationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_GetIntegration")]
    public async Task<IActionResult> Get(Guid integrationId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        IntegrationMonitoringQueries.Window(from, to, "to", errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Respond(await catalogue.GetAsync(integrationId, from, to, cancellationToken));
    }

    /// <summary>DISABLED → ACTIVE: monitored again. Changes no adapter (BR-INT-008).</summary>
    [HttpPost("{integrationId:guid}/enable")]
    [RequirePermission(PermissionCatalogue.IntegrationConfigure)]
    [SensitiveWrite]
    [ProducesResponseType<IntegrationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_EnableIntegration")]
    public Task<IActionResult> Enable(Guid integrationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => catalogue.EnableAsync(CallerId, integrationId, version, ct), cancellationToken);

    /// <summary>ACTIVE → DISABLED: telemetry is still recorded; no alert is raised and FG-05 runs none of its operations.</summary>
    [HttpPost("{integrationId:guid}/disable")]
    [RequirePermission(PermissionCatalogue.IntegrationConfigure)]
    [SensitiveWrite]
    [ProducesResponseType<IntegrationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_DisableIntegration")]
    public Task<IActionResult> Disable(Guid integrationId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => catalogue.DisableAsync(CallerId, integrationId, version, ct), cancellationToken);

    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<IntegrationDetail>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
