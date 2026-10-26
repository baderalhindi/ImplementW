namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The one consumer of every NOTIFICATION_INTENT (event-conventions §3: Notifications, only). The outbox dispatcher calls
/// it after the producer committed, inside the transaction that marks the message dispatched, as it calls an
/// <see cref="IDomainEventConsumer"/>. What it saves commits with that mark; a failure leaves both undone and the
/// message is tried again. Nothing it does can reach the producer's transaction, which had committed before.
/// </summary>
public interface INotificationIntentConsumer
{
    /// <param name="payload">The serialised <see cref="NotificationIntentEnvelope"/>.</param>
    /// <param name="cancellationToken">Cancels the dispatch; nothing is committed.</param>
    public Task HandleAsync(string payload, CancellationToken cancellationToken);
}
