namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The one consumer of a DOMAIN_EVENT type (event-conventions EV-6). The dispatcher calls it inside a transaction that
/// also marks the message dispatched, so what the handler saves through the request's context commits exactly when the
/// message counts as delivered. A handler that throws leaves both undone, and the message is tried again.
/// </summary>
public interface IDomainEventConsumer
{
    /// <summary>The <c>eventType</c> it handles; one handler per type.</summary>
    public string EventType { get; }

    /// <param name="payload">The serialised envelope, read with <see cref="EventSerialization.Deserialize{TEvent}"/>.</param>
    /// <param name="cancellationToken">Cancels the dispatch; nothing is committed.</param>
    public Task HandleAsync(string payload, CancellationToken cancellationToken);
}
