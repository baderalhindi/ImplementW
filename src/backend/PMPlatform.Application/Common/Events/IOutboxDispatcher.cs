namespace PMPlatform.Application.Common.Events;

/// <summary>
/// Delivers committed outbox messages to their consumers (event-conventions EV-6): at most five attempts with
/// exponential back-off; a message already dispatched, or being dispatched by another process, is skipped.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>Dispatches up to <paramref name="batchSize"/> messages that are due, oldest first; returns how many were delivered.</summary>
    public Task<int> DispatchDueAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Delivers one message now, whether or not it is due. True if this call delivered it; false if it was already
    /// delivered, is being delivered elsewhere, or its consumer failed (the failure is recorded for the next attempt).
    /// </summary>
    public Task<bool> DispatchAsync(Guid messageId, CancellationToken cancellationToken);
}
