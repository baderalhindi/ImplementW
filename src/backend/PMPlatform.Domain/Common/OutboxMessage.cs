namespace PMPlatform.Domain.Common;

/// <summary>
/// The transactional outbox (ERD <c>common.outbox_message</c>, D-17; event-conventions EV-6): a message written in the
/// producer's transaction and dispatched after commit. Infrastructure, not a module. Delete policy: RETAIN.
/// </summary>
public sealed class OutboxMessage : AuditedEntity
{
    public required string SourceModule { get; set; }

    public EventKind MessageType { get; set; }

    /// <summary><c>&lt;eventType&gt;:&lt;idempotencyKey&gt;</c>, unique with <see cref="MessageType"/>: a message cannot be published twice.</summary>
    public required string MessageKey { get; set; }

    /// <summary>The serialised envelope; never queried by attribute.</summary>
    public required string Payload { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset? DispatchedAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? NextAttemptAt { get; set; }

    public string? LastError { get; set; }
}
