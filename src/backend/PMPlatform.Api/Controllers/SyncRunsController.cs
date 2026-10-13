using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.IntegrationMonitoring;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;

namespace PMPlatform.Api.Controllers;

/// <summary>FG-05 ADM-047's synchronisation and reconciliation runs with their counts (TASK-075).</summary>
[Route("api/v1/sync-runs")]
[Tags("IntegrationMonitoring")]
public sealed class SyncRunsController(ISyncRunService runs) : AdministrationControllerBase
{
    /// <summary>Newest first. Filters: <c>integrationId</c>, <c>runType</c> and <c>status</c> (sets), <c>correlationId</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<SyncRunPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_ListSyncRuns")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? integrationId, [FromQuery] string? runType, [FromQuery] string? status, [FromQuery] Guid? correlationId,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        SyncRunQuery query = IntegrationMonitoringQueries.SyncRuns(integrationId, runType, status, correlationId, errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await runs.SearchAsync(query, paging, cancellationToken));
    }

    [HttpGet("{syncRunId:guid}")]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<SyncRunView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_GetSyncRun")]
    public async Task<IActionResult> Get(Guid syncRunId, CancellationToken cancellationToken) => Respond(await runs.GetAsync(syncRunId, cancellationToken));
}

/// <summary>
/// FG-05 US-INT-OPS-047 (TASK-075): reconcile an integration. The run compares the source with the platform's copy and reports each mismatch as
/// an operational alert for investigation; it changes no authoritative data (BR-INT-006). The request's <c>Idempotency-Key</c> is the run's
/// key: the same key again answers the run it began and starts nothing.
/// </summary>
[Route(Collection)]
[Tags("IntegrationMonitoring")]
public sealed class IntegrationReconciliationsController(ISyncRunService runs) : AdministrationControllerBase
{
    private const string Collection = "api/v1/integration-reconciliations";

    /// <summary>
    /// 201 with the lineage's first attempt (or, for a repeated key, its latest) and its run; <c>Location</c> is the attempt. 422
    /// <c>RECONCILIATION_NOT_AVAILABLE</c> or <c>INTEGRATION_DISABLED</c>; 503 when the source is not configured.
    /// </summary>
    [HttpPost]
    [RequirePermission(PermissionCatalogue.IntegrationOperate)]
    [SensitiveWrite]
    [ProducesResponseType<InvocationDetail>(StatusCodes.Status201Created, "application/json")]
    [EndpointName("IntegrationMonitoring_CreateIntegrationReconciliation")]
    public async Task<IActionResult> Create(IntegrationReconciliationCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { Count: > 0 } errors)
        {
            return ValidationFailed(errors);
        }

        string requestKey = Request.Headers[SensitiveWriteAttribute.HeaderName]!;
        AdministrationResult<InvocationDetail> result = await runs.ReconcileAsync(CallerId, request.IntegrationId!.Value, requestKey, cancellationToken);
        return result.Succeeded
            ? Created($"/api/v1/integration-invocations/{result.Value.Invocation.Id}", result.Value)
            : Failure(result.Error);
    }
}
