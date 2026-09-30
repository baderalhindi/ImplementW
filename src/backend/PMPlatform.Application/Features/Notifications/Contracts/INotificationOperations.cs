using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.Notifications.Contracts;

/// <summary>
/// WF-15 operations (TASK-039): what was received, where it went, what failed. A FAILED intent is redriven once its
/// configuration or template exists; a dead-lettered delivery once its channel is back. Both are audited.
/// </summary>
public interface INotificationOperations
{
    public Task<NotificationIntentPage> ListIntentsAsync(NotificationIntentQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<NotificationIntentDetail>> GetIntentAsync(Guid intentId, CancellationToken cancellationToken);

    /// <summary>FAILED → RECEIVED, routed on the next pass. Any other state is 409 INVALID_TRANSITION.</summary>
    public Task<AdministrationResult<NotificationIntentDetail>> RedriveIntentAsync(Guid actorId, Guid intentId, CancellationToken cancellationToken);

    public Task<NotificationDeliveryPage> ListDeliveriesAsync(NotificationDeliveryQuery query, CancellationToken cancellationToken);

    /// <summary>DEAD_LETTER → PENDING with its attempts reset, sent on the next pass. Any other state is 409 INVALID_TRANSITION.</summary>
    public Task<AdministrationResult<NotificationDeliverySummary>> RedriveDeliveryAsync(Guid actorId, Guid deliveryId, CancellationToken cancellationToken);
}
