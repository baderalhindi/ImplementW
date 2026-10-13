using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts;
using PMPlatform.Application.Features.IntegrationMonitoring.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IntegrationMonitoring;

namespace PMPlatform.Application.Features.IntegrationMonitoring;

/// <summary>
/// ADM-047's operational alerts and their handling (FG-05 §10.2, §13.3). Handling changes the alert only: the integration's health, its
/// attempts and every source record stay as they are (BR-INT-037). Each action is integration audit.
/// </summary>
internal sealed partial class OperationalAlertService(
    IIntegrationMonitoringRepository repository, IAuditTrail audit, TimeProvider timeProvider, ILogger<OperationalAlertService> logger) : IOperationalAlertService
{
    public async Task<OperationalAlertPage> SearchAsync(OperationalAlertQuery query, PageRequest page, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        (IReadOnlyList<OperationalAlert> items, int total) = await repository.SearchAlertsAsync(query, page, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, string> codes = await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false);
        return new OperationalAlertPage([.. items.Select(a => IntegrationMonitoringMapping.ToView(a, codes))], page.Page, page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<OperationalAlertView>>> GetAsync(Guid alertId, CancellationToken cancellationToken) =>
        await repository.FindAlertAsync(alertId, null, cancellationToken).ConfigureAwait(false) is { } alert
            ? await ViewAsync(alert, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public Task<AdministrationResult<Versioned<OperationalAlertView>>> AcknowledgeAsync(Guid actorId, Guid alertId, uint? expectedVersion, CancellationToken cancellationToken) =>
        HandleAsync(actorId, alertId, expectedVersion, OperationalAlertStatus.Acknowledged, null, IntegrationMonitoringAuditEvents.AlertAcknowledged, cancellationToken);

    public Task<AdministrationResult<Versioned<OperationalAlertView>>> ResolveAsync(
        Guid actorId, Guid alertId, NarrativeText note, uint? expectedVersion, CancellationToken cancellationToken) =>
        HandleAsync(actorId, alertId, expectedVersion, OperationalAlertStatus.Resolved, note, IntegrationMonitoringAuditEvents.AlertResolved, cancellationToken);

    public Task<AdministrationResult<Versioned<OperationalAlertView>>> DismissAsync(
        Guid actorId, Guid alertId, NarrativeText note, uint? expectedVersion, CancellationToken cancellationToken) =>
        HandleAsync(actorId, alertId, expectedVersion, OperationalAlertStatus.Dismissed, note, IntegrationMonitoringAuditEvents.AlertDismissed, cancellationToken);

    private async Task<AdministrationResult<Versioned<OperationalAlertView>>> HandleAsync(
        Guid actorId, Guid alertId, uint? expectedVersion, OperationalAlertStatus target, NarrativeText? note, string eventType, CancellationToken cancellationToken)
    {
        if (await repository.FindAlertAsync(alertId, expectedVersion, cancellationToken).ConfigureAwait(false) is not { } alert)
        {
            return AdministrationError.NotFound;
        }

        if (alert.Status is OperationalAlertStatus.Resolved or OperationalAlertStatus.Dismissed)
        {
            return AdministrationError.TerminalState;
        }

        if (target == OperationalAlertStatus.Acknowledged && alert.Status != OperationalAlertStatus.Open)
        {
            return AdministrationError.InvalidTransition;
        }

        OperationalAlertStatus before = alert.Status;
        DateTimeOffset now = timeProvider.GetUtcNow();
        alert.Status = target;
        if (target == OperationalAlertStatus.Acknowledged)
        {
            alert.AcknowledgedByUserId = actorId;
            alert.AcknowledgedAt = now;
        }
        else
        {
            alert.ResolvedAt = now;
            alert.ResolutionNote = note;
        }

        alert.UpdatedAt = now;
        alert.UpdatedBy = actorId;
        audit.Stage(IntegrationMonitoringAudit.AlertHandled(eventType, actorId, alert, before));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != IntegrationMonitoringSaveOutcome.Saved)
        {
            return AdministrationError.PreconditionFailed;
        }

        LogHandled(logger, actorId, alert.Id, target);
        return await ViewAsync(alert, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Versioned<OperationalAlertView>> ViewAsync(OperationalAlert alert, CancellationToken cancellationToken) =>
        new(IntegrationMonitoringMapping.ToView(alert, await repository.GetIntegrationCodesAsync(cancellationToken).ConfigureAwait(false)), repository.RowVersionOf(alert));

    [LoggerMessage(Level = LogLevel.Information, Message = "Integration monitoring: {ActorId} made alert {AlertId} {Status}.")]
    private static partial void LogHandled(ILogger logger, Guid actorId, Guid alertId, OperationalAlertStatus status);
}
