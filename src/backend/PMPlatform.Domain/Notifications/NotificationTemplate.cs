using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Notifications;

/// <summary>
/// The bilingual, mandatory text of one event type on one channel (ADR-012, ADR-004), under the governed lifecycle. A
/// new wording is a new version; the PUBLISHED version of an event type and channel is the one rendered. An SMS variant
/// carries the event and its deep link only. Delete policy: RETAIN.
/// </summary>
public sealed class NotificationTemplate : GovernedEntity
{
    /// <summary>The FG-04 <c>NotificationEventFamily</c> code the event type belongs to.</summary>
    public required string EventFamilyCode { get; set; }

    /// <summary>The intent's <c>eventType</c>, <c>&lt;ProducerModule&gt;.&lt;EventName&gt;</c> (event-conventions EV-1).</summary>
    public required string EventType { get; set; }

    public NotificationChannel Channel { get; set; }

    public int VersionNo { get; set; }

    /// <summary>Email and in-app only; null for SMS.</summary>
    public string? SubjectAr { get; set; }

    public string? SubjectEn { get; set; }

    public required string BodyAr { get; set; }

    public required string BodyEn { get; set; }
}
