using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// A formal audit record (FG-06, Blueprint Section 18; TASK-033, TASK-073). Delete policy: APPEND_ONLY. The database
/// sets <see cref="RecordedAt"/>, <see cref="PreviousEventHash"/> and <see cref="EventHash"/> on insert and refuses
/// every update and delete, so the application can neither choose a place in the hash chain nor rewrite one.
/// </summary>
public sealed class AuditEvent : AuditedEntity
{
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Set by the database when the row is inserted.</summary>
    public DateTimeOffset RecordedAt { get; set; }

    public AuditEventClass EventClass { get; set; }

    /// <summary><c>&lt;ProducerModule&gt;.&lt;EventName&gt;</c> (event-conventions EV-1).</summary>
    public required string EventType { get; set; }

    public AuditActorType ActorType { get; set; }

    /// <summary>Null when the actor is not known: an anonymous caller, or a sign-in whose person was not identified.</summary>
    public Guid? ActorUserId { get; set; }

    public string? SubjectModule { get; set; }

    public string? SubjectType { get; set; }

    public Guid? SubjectId { get; set; }

    public Guid? ScopeProjectId { get; set; }

    public Guid? ScopeExternalEntityId { get; set; }

    public Guid CorrelationId { get; set; }

    public AuditOutcome Outcome { get; set; }

    public Guid? DataClassificationItemId { get; set; }

    /// <summary>Set by the database: the hash of the event recorded before this one, null for the first.</summary>
    public string? PreviousEventHash { get; set; }

    /// <summary>Set by the database: SHA-256 over this row and <see cref="PreviousEventHash"/>.</summary>
    public string? EventHash { get; set; }
}
