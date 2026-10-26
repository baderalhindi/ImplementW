using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Notifications;

/// <summary>
/// A typed intent received from a source module after the source committed (commit-before-notify, Blueprint Section
/// 15). Unique on (source event type, source reference), which is the producer's idempotency key (event-conventions
/// EV-4). Delete policy: RETAIN.
/// </summary>
public sealed class NotificationIntent : AuditedEntity
{
    public required string SourceModule { get; set; }

    public required string SourceEventType { get; set; }

    /// <summary>The source's idempotency key, e.g. a <c>ConcernEscalation</c> id.</summary>
    public required string SourceReference { get; set; }

    public required string EventFamilyCode { get; set; }

    public string? SubjectType { get; set; }

    /// <summary>Identifier only.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>Scope anchor for the eligibility recheck.</summary>
    public Guid? ScopeProjectId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Reminders; the source condition is revalidated before send.</summary>
    public DateTimeOffset? ScheduledFor { get; set; }

    public DateTimeOffset? ConditionRevalidatedAt { get; set; }

    public NotificationIntentStatus Status { get; set; }

    /// <summary>Why the intent sent nothing: SUPPRESSED's rule, or FAILED's refusal.</summary>
    public string? SuppressionReason { get; set; }

    /// <summary>An SPA route, never a URL with a token (EV-10).</summary>
    public string? DeepLink { get; set; }
}
