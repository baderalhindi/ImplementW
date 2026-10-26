using Microsoft.Extensions.Logging;

namespace PMPlatform.Application.Features.Notifications;

/// <summary>
/// The WF-15 pass. Each intent and each delivery is handled in a transaction of its own, so one that cannot be handled —
/// a condition source or the database briefly unavailable — is rolled back, logged and tried on a later pass without
/// holding up the rest.
/// </summary>
internal sealed partial class NotificationProcessing(
    INotificationRepository repository,
    NotificationRouting routing,
    NotificationSending sending,
    TimeProvider timeProvider,
    ILogger<NotificationProcessing> logger) : INotificationProcessing
{
    public async Task<int> RunAsync(int batchSize, CancellationToken cancellationToken)
    {
        int handled = 0;
        foreach (Guid intentId in await repository.FindDueIntentIdsAsync(timeProvider.GetUtcNow(), batchSize, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                handled += await routing.RouteAsync(intentId, cancellationToken).ConfigureAwait(false) ? 1 : 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await repository.AbandonAsync().ConfigureAwait(false);
                await repository.TouchIntentAsync(intentId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                LogNotRouted(logger, intentId, exception.GetType().Name);
            }
        }

        foreach (Guid deliveryId in await repository.FindDueDeliveryIdsAsync(timeProvider.GetUtcNow(), batchSize, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                handled += await sending.SendAsync(deliveryId, cancellationToken).ConfigureAwait(false) ? 1 : 0;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await repository.AbandonAsync().ConfigureAwait(false);
                LogNotSent(logger, deliveryId, exception.GetType().Name);
            }
        }

        return handled + await repository.CompleteRoutedIntentsAsync(timeProvider.GetUtcNow(), batchSize, cancellationToken).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification intent {IntentId} was not routed: {Reason}. It is tried again on a later pass.")]
    private static partial void LogNotRouted(ILogger logger, Guid intentId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Notification delivery {DeliveryId} was not attempted: {Reason}. It is tried again on a later pass.")]
    private static partial void LogNotSent(ILogger logger, Guid deliveryId, string reason);
}
