using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.IntegrationMonitoring;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-05 ADM-048 Integration Logs (TASK-075): attempts searched by integration, state, operation, correlation id, lineage key and business
/// reference — codes and times, never a payload — and the operator's retry of a lineage FG-05 runs.
/// </summary>
[Route(Collection)]
[Tags("IntegrationMonitoring")]
public sealed class IntegrationInvocationsController(IIntegrationInvocationService invocations) : AdministrationControllerBase
{
    private const string Collection = "api/v1/integration-invocations";

    /// <summary>
    /// Newest first. Filters: <c>integrationId</c>, <c>status</c> (a set), <c>operationCode</c>, <c>correlationId</c> (a request's whole
    /// technical lineage), <c>idempotencyKey</c>, <c>sourceReference</c>, <c>startedFrom</c>/<c>startedTo</c>.
    /// </summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.IntegrationLogView)]
    [ProducesResponseType<InvocationPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_ListIntegrationInvocations")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? integrationId, [FromQuery] string? status, [FromQuery] string? operationCode, [FromQuery] Guid? correlationId,
        [FromQuery] string? idempotencyKey, [FromQuery] string? sourceReference, [FromQuery] DateTimeOffset? startedFrom, [FromQuery] DateTimeOffset? startedTo,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        InvocationQuery query = IntegrationMonitoringQueries.Invocations(
            integrationId, status, operationCode, correlationId, idempotencyKey, sourceReference, startedFrom, startedTo, errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await invocations.SearchAsync(query, paging, cancellationToken));
    }

    /// <summary>The attempt with every attempt of its lineage, and whether FG-05 would retry it now.</summary>
    [HttpGet("{invocationId:guid}")]
    [RequirePermission(PermissionCatalogue.IntegrationLogView)]
    [ProducesResponseType<InvocationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_GetIntegrationInvocation")]
    public async Task<IActionResult> Get(Guid invocationId, CancellationToken cancellationToken) =>
        Respond(await invocations.GetAsync(invocationId, cancellationToken));

    /// <summary>
    /// Runs the lineage's next attempt now, under its own correlation id and key. Only from the latest attempt of a lineage FG-05 runs that
    /// is dead-lettered or waiting for a retry: 409 <c>INVOCATION_ALREADY_SUCCEEDED</c> or <c>INVOCATION_NOT_RETRYABLE</c> otherwise, 422
    /// <c>RETRY_NOT_PERMITTED</c> for an attempt another module made, <c>INTEGRATION_DISABLED</c> when monitoring is off.
    /// </summary>
    [HttpPost("{invocationId:guid}/retry")]
    [RequirePermission(PermissionCatalogue.IntegrationOperate)]
    [SensitiveWrite]
    [ProducesResponseType<InvocationDetail>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_RetryIntegrationInvocation")]
    public async Task<IActionResult> Retry(Guid invocationId, CancellationToken cancellationToken) =>
        Respond(await invocations.RetryAsync(CallerId, invocationId, cancellationToken));
}
