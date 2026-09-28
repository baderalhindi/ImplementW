using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Messaging;

/// <summary>Adds the message to the request's context, so the producer's save commits it with the fact it reports (EV-6).</summary>
internal sealed class Outbox(PMPlatformDbContext context) : IOutbox
{
    public void Stage<TData>(EventEnvelope<TData> envelope)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // ERD D-2: a service principal is a user, so the actor is the row's author; INTEGRATION has none and is not a producer here.
        Guid author = envelope.Actor.UserId ?? throw new ArgumentException("An outbox message needs an acting user or service principal.", nameof(envelope));
        context.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            SourceModule = envelope.SourceModule,
            MessageType = envelope.Kind,
            MessageKey = envelope.MessageKey,
            Payload = EventSerialization.Serialize(envelope),
            OccurredAt = envelope.OccurredAt,
            CreatedAt = envelope.OccurredAt,
            CreatedBy = author,
            UpdatedAt = envelope.OccurredAt,
            UpdatedBy = author,
        });
    }
}
