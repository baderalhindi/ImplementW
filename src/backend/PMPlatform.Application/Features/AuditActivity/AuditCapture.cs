using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Events;
using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.AuditActivity;

/// <summary>
/// An audit event as it is captured in the producer's transaction: the payload of its <c>AUDIT_EVENT</c> outbox message
/// (TASK-073, M-6). It is AuditActivity's own record, written and read only by AuditActivity, not the cross-module envelope:
/// an audit event may have no subject and no known actor (a refused sign-in), which the envelope requires (record D-3).
/// <see cref="EventId"/> becomes the stored event's id, so a repeated delivery cannot store it twice (AUD-CC-07).
/// </summary>
public sealed record AuditCapture(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    AuditEventClass EventClass,
    string EventType,
    AuditOutcome Outcome,
    AuditActorType ActorType,
    Guid? ActorUserId,
    AuditSubject? Subject,
    Guid? ScopeProjectId,
    Guid? ScopeExternalEntityId,
    Guid? DataClassificationItemId,
    Guid CorrelationId,
    IReadOnlyList<AuditAttribute> Attributes)
{
    /// <summary>The version of this record's shape; a message of any other version is refused at ingestion (AUD-CC-29).</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The module that produced the event: the part of <see cref="EventType"/> before the dot (EV-1).</summary>
    public string SourceModule => EventType[..Math.Max(EventType.IndexOf('.', StringComparison.Ordinal), 0)];

    /// <summary>The outbox message key (EV-4): the event type and the event id, unique with the message type.</summary>
    public string MessageKey => EventMessageKey.Of(EventType, EventId.ToString("D"));

    public string Serialize() => System.Text.Json.JsonSerializer.Serialize(this, EventSerialization.Options);

    /// <exception cref="System.Text.Json.JsonException">The payload is not an audit capture.</exception>
    public static AuditCapture Deserialize(string payload) => EventSerialization.Deserialize<AuditCapture>(payload);
}
