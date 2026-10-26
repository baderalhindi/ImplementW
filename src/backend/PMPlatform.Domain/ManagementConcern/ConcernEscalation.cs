using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ManagementConcern;

/// <summary>
/// One escalation of a concern to a higher authority, numbered within the concern. Its id is the idempotency key of the single
/// NotificationIntent it produces (TASK-057). It never changes the concern's status (WF-07 ISS-GP-06). Delete policy: RETAIN.
/// </summary>
public sealed class ConcernEscalation : AuditedEntity
{
    public Guid ManagementConcernId { get; set; }

    public int EscalationNo { get; set; }

    /// <summary>An internal user: entities raise concerns, they do not escalate them (ADR-013).</summary>
    public Guid EscalatedByUserId { get; set; }

    public DateTimeOffset EscalatedAt { get; set; }

    /// <summary>The role the escalation is addressed to, as WORKFLOW_POLICY routed it when it was raised.</summary>
    public Guid EscalatedToRoleId { get; set; }

    public required NarrativeText Reason { get; set; }

    public ConcernEscalationStatus Status { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>Who resolved or withdrew it.</summary>
    public Guid? ResolvedByUserId { get; set; }

    public NarrativeText? Resolution { get; set; }

    /// <summary>The escalator's <c>Idempotency-Key</c>: a retry with the same key finds this escalation instead of raising another.</summary>
    public Guid RequestKey { get; set; }
}
