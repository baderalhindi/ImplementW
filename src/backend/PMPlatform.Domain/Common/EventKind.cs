namespace PMPlatform.Domain.Common;

/// <summary>The three kinds of cross-module message (event-conventions §3): the envelope's <c>kind</c> and <c>outbox_message.message_type</c>.</summary>
public enum EventKind
{
    DomainEvent = 1,
    NotificationIntent = 2,
    AuditEvent = 3,
}
