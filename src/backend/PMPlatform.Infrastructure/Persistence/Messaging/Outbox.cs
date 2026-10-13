using Microsoft.EntityFrameworkCore;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Messaging;

/// <summary>
/// <see cref="Stage{TData}"/> adds the message to the request's context, so the producer's save commits it with the fact it reports
/// (EV-6). <see cref="RecordAsync{TData}"/> commits it on a context of its own, so nothing pending in the request goes with it.
/// </summary>
internal sealed class Outbox(PMPlatformDbContext context, DbContextOptions<PMPlatformDbContext> options) : IOutbox
{
    public void Stage<TData>(EventEnvelope<TData> envelope)
        where TData : class =>
        context.Add(MessageOf(envelope));

    public async Task RecordAsync<TData>(EventEnvelope<TData> envelope, CancellationToken cancellationToken)
        where TData : class
    {
        OutboxMessage message = MessageOf(envelope);
        await using PMPlatformDbContext own = new(options);
        own.Add(message);
        await own.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static OutboxMessage MessageOf<TData>(EventEnvelope<TData> envelope)
        where TData : class
    {
        ArgumentNullException.ThrowIfNull(envelope);

        // ERD D-2: a service principal is a user, so the actor is the row's author; INTEGRATION has none and is not a producer here.
        Guid author = envelope.Actor.UserId ?? throw new ArgumentException("An outbox message needs an acting user or service principal.", nameof(envelope));
        return new OutboxMessage
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
        };
    }
}
