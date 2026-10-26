using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.Events;

/// <summary>
/// The one envelope every cross-module message carries (event-conventions EV-2; <c>event-envelope.schema.json</c>). It
/// is the whole <c>outbox_message.payload</c>. Every event type derives from it with its own <typeparamref name="TData"/>
/// (EV-8), declared in its producer's <c>Contracts/Events</c>.
/// </summary>
public abstract record EventEnvelope<TData>
    where TData : class
{
    public required Guid EventId { get; init; }

    /// <summary><c>&lt;ProducerModule&gt;.&lt;EventName&gt;</c> (EV-1).</summary>
    public required string EventType { get; init; }

    /// <summary>The version of <see cref="Data"/>'s schema (EV-7).</summary>
    public required int SchemaVersion { get; init; }

    public required EventKind Kind { get; init; }

    /// <summary><c>&lt;eventType&gt;:&lt;idempotencyKey&gt;</c>, unique with <see cref="Kind"/> (EV-4).</summary>
    public required string MessageKey { get; init; }

    /// <summary>The producer's natural key for the occurrence (EV-4): never a fresh uuid, the HTTP key or the correlation id.</summary>
    public required string IdempotencyKey { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }

    public required string SourceModule { get; init; }

    public required Guid CorrelationId { get; init; }

    public Guid? CausationId { get; init; }

    public required EventActor Actor { get; init; }

    public required EventSubject Subject { get; init; }

    public required EventScope Scope { get; init; }

    public required TData Data { get; init; }
}
