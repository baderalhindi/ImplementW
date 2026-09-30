using Microsoft.Extensions.Logging;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Domain.Notifications;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>WF-15 operations: the received intents and their deliveries, and the two redrives. A redrive is audited.</summary>
internal sealed partial class NotificationOperations(
    INotificationRepository repository, IAuditTrail audit, TimeProvider timeProvider, ILogger<NotificationOperations> logger) : INotificationOperations
{
    public async Task<NotificationIntentPage> ListIntentsAsync(NotificationIntentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        (IReadOnlyList<NotificationIntent> items, int total) = await repository.ListIntentsAsync(query, cancellationToken).ConfigureAwait(false);
        return new NotificationIntentPage([.. items.Select(NotificationMapping.ToSummary)], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<AdministrationResult<NotificationIntentDetail>> GetIntentAsync(Guid intentId, CancellationToken cancellationToken) =>
        await repository.FindIntentAsync(intentId, cancellationToken).ConfigureAwait(false) is { } intent
            ? await DetailAsync(intent, cancellationToken).ConfigureAwait(false)
            : AdministrationError.NotFound;

    public async Task<AdministrationResult<NotificationIntentDetail>> RedriveIntentAsync(Guid actorId, Guid intentId, CancellationToken cancellationToken)
    {
        if (await repository.FindIntentAsync(intentId, cancellationToken).ConfigureAwait(false) is not { } intent)
        {
            return AdministrationError.NotFound;
        }

        if (intent.Status != NotificationIntentStatus.Failed)
        {
            return AdministrationError.InvalidTransition;
        }

        string? reason = intent.SuppressionReason;
        DateTimeOffset now = timeProvider.GetUtcNow();
        intent.Status = NotificationIntentStatus.Received;
        intent.SuppressionReason = null;
        intent.UpdatedAt = now;
        intent.UpdatedBy = actorId;
        audit.Stage(NotificationAudit.IntentRedriven(actorId, intent, reason));
        if (NotificationRefusal.Of(await repository.SaveAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            return AdministrationError.PreconditionFailed;
        }

        LogRedriven(logger, actorId, nameof(NotificationIntent), intent.Id);
        return await DetailAsync(intent, cancellationToken).ConfigureAwait(false);
    }

    public async Task<NotificationDeliveryPage> ListDeliveriesAsync(NotificationDeliveryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        (IReadOnlyList<NotificationDelivery> items, int total) = await repository.ListDeliveriesAsync(query, cancellationToken).ConfigureAwait(false);
        return new NotificationDeliveryPage([.. items.Select(NotificationMapping.ToSummary)], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<AdministrationResult<NotificationDeliverySummary>> RedriveDeliveryAsync(Guid actorId, Guid deliveryId, CancellationToken cancellationToken)
    {
        if (await repository.FindDeliveryAsync(deliveryId, cancellationToken).ConfigureAwait(false) is not { } delivery)
        {
            return AdministrationError.NotFound;
        }

        if (delivery.Status != NotificationDeliveryStatus.DeadLetter)
        {
            return AdministrationError.InvalidTransition;
        }

        int attempts = delivery.AttemptCount;
        DateTimeOffset now = timeProvider.GetUtcNow();
        delivery.Status = NotificationDeliveryStatus.Pending;
        delivery.AttemptCount = 0;
        delivery.NextAttemptAt = now;
        delivery.DeadLetteredAt = null;
        delivery.UpdatedAt = now;
        delivery.UpdatedBy = actorId;

        // The intent is open again until this delivery ends.
        NotificationIntent intent = (await repository.FindIntentAsync(delivery.NotificationIntentId, cancellationToken).ConfigureAwait(false))!;
        if (intent.Status == NotificationIntentStatus.Completed)
        {
            intent.Status = NotificationIntentStatus.Routed;
            intent.UpdatedAt = now;
            intent.UpdatedBy = actorId;
        }

        audit.Stage(NotificationAudit.DeliveryRedriven(actorId, delivery, attempts));
        if (NotificationRefusal.Of(await repository.SaveAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            return AdministrationError.PreconditionFailed;
        }

        LogRedriven(logger, actorId, nameof(NotificationDelivery), delivery.Id);
        return NotificationMapping.ToSummary(delivery);
    }

    private async Task<NotificationIntentDetail> DetailAsync(NotificationIntent intent, CancellationToken cancellationToken) =>
        new(NotificationMapping.ToSummary(intent), intent.SubjectType, intent.SubjectId, intent.ScopeProjectId,
            [.. (await repository.GetDeliveriesAsync(intent.Id, cancellationToken).ConfigureAwait(false)).Select(NotificationMapping.ToSummary)]);

    [LoggerMessage(Level = LogLevel.Information, Message = "Notification operations: {ActorId} redrove {Type} {Id}.")]
    private static partial void LogRedriven(ILogger logger, Guid actorId, string type, Guid id);
}
