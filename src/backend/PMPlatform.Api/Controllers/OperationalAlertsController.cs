using Microsoft.AspNetCore.Mvc;
using PMPlatform.Api.Authorization;
using PMPlatform.Api.Errors;
using PMPlatform.Api.Models.IdentityAccess;
using PMPlatform.Api.Models.IntegrationMonitoring;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Domain.Common;

namespace PMPlatform.Api.Controllers;

/// <summary>
/// FG-05 ADM-047's operational alerts and their handling (TASK-075; FG-05 §10.2): acknowledge, resolve and dismiss change the alert only, never
/// the integration's health, its attempts or a source record.
/// </summary>
[Route(Collection)]
[Tags("IntegrationMonitoring")]
public sealed class OperationalAlertsController(IOperationalAlertService alerts) : AdministrationControllerBase
{
    private const string Collection = "api/v1/operational-alerts";

    /// <summary>Newest first. Filters: <c>integrationId</c>, <c>status</c> and <c>alertType</c> (sets), <c>raisedFrom</c>/<c>raisedTo</c>.</summary>
    [HttpGet]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<OperationalAlertPage>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_ListOperationalAlerts")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? integrationId, [FromQuery] string? status, [FromQuery] string? alertType, [FromQuery] DateTimeOffset? raisedFrom,
        [FromQuery] DateTimeOffset? raisedTo, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        List<FieldError> errors = [];
        OperationalAlertQuery query = IntegrationMonitoringQueries.Alerts(integrationId, status, alertType, raisedFrom, raisedTo, errors);
        PageRequest paging = QueryParameters.Page(page, pageSize, errors);
        return errors.Count > 0 ? ValidationFailed(errors) : Ok(await alerts.SearchAsync(query, paging, cancellationToken));
    }

    [HttpGet("{alertId:guid}")]
    [RequirePermission(PermissionCatalogue.IntegrationView)]
    [ProducesResponseType<OperationalAlertView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_GetOperationalAlert")]
    public async Task<IActionResult> Get(Guid alertId, CancellationToken cancellationToken) => Respond(await alerts.GetAsync(alertId, cancellationToken));

    /// <summary>OPEN → ACKNOWLEDGED, recording who and when. It does not claim the integration recovered.</summary>
    [HttpPost("{alertId:guid}/acknowledge")]
    [RequirePermission(PermissionCatalogue.IntegrationAlertManage)]
    [SensitiveWrite]
    [ProducesResponseType<OperationalAlertView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_AcknowledgeOperationalAlert")]
    public Task<IActionResult> Acknowledge(Guid alertId, CancellationToken cancellationToken) =>
        CommandAsync((version, ct) => alerts.AcknowledgeAsync(CallerId, alertId, version, ct), cancellationToken);

    /// <summary>OPEN or ACKNOWLEDGED → RESOLVED with the operator's note (required).</summary>
    [HttpPost("{alertId:guid}/resolve")]
    [RequirePermission(PermissionCatalogue.IntegrationAlertManage)]
    [SensitiveWrite]
    [ProducesResponseType<OperationalAlertView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_ResolveOperationalAlert")]
    public Task<IActionResult> Resolve(Guid alertId, OperationalAlertNoteCommand command, CancellationToken cancellationToken) =>
        NoteCommandAsync(command, (note, version, ct) => alerts.ResolveAsync(CallerId, alertId, note, version, ct), cancellationToken);

    /// <summary>OPEN or ACKNOWLEDGED → DISMISSED with the operator's note (required): the condition was not real.</summary>
    [HttpPost("{alertId:guid}/dismiss")]
    [RequirePermission(PermissionCatalogue.IntegrationAlertManage)]
    [SensitiveWrite]
    [ProducesResponseType<OperationalAlertView>(StatusCodes.Status200OK, "application/json")]
    [EndpointName("IntegrationMonitoring_DismissOperationalAlert")]
    public Task<IActionResult> Dismiss(Guid alertId, OperationalAlertNoteCommand command, CancellationToken cancellationToken) =>
        NoteCommandAsync(command, (note, version, ct) => alerts.DismissAsync(CallerId, alertId, note, version, ct), cancellationToken);

    private async Task<IActionResult> NoteCommandAsync(
        OperationalAlertNoteCommand command,
        Func<NarrativeText, uint?, CancellationToken, Task<AdministrationResult<Versioned<OperationalAlertView>>>> handle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Validate(out NarrativeText? note) is { Count: > 0 } errors
            ? ValidationFailed(errors)
            : await CommandAsync((version, ct) => handle(note!, version, ct), cancellationToken);
    }

    private async Task<IActionResult> CommandAsync(
        Func<uint?, CancellationToken, Task<AdministrationResult<Versioned<OperationalAlertView>>>> command, CancellationToken cancellationToken) =>
        TryReadIfMatch(required: false, out uint? version, out IActionResult? problem)
            ? Respond(await command(version, cancellationToken))
            : problem!;
}
