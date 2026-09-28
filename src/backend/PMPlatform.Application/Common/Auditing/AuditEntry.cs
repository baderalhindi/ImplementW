using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Common.Auditing;

/// <summary>
/// What a producer states about an audited occurrence (event-conventions EV-9). The attributes arrive already redacted:
/// no password, token, second-factor code or secret is ever one of them, and personal contact details are
/// <see cref="AuditAttribute.Withheld"/>. The time, the correlation id and the client address are added by the trail.
/// </summary>
public sealed record AuditEntry(AuditEventClass EventClass, string EventType, AuditOutcome Outcome)
{
    /// <summary>Null when the actor is not known; the event is then attributed to the audit-capture service principal.</summary>
    public Guid? ActorUserId { get; init; }

    public AuditActorType ActorType { get; init; } = AuditActorType.User;

    public AuditSubject? Subject { get; init; }

    public Guid? ScopeProjectId { get; init; }

    public Guid? ScopeExternalEntityId { get; init; }

    public Guid? DataClassificationItemId { get; init; }

    public IReadOnlyList<AuditAttribute> Attributes { get; init; } = [];
}
