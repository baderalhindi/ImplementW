using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.AuditActivity;

/// <summary>
/// One line of a project's business-readable Activity timeline (SCR-057; FG-06 §7; TASK-073), derived from one
/// <see cref="AuditEvent"/> by the Activity template catalogue. Never the evidence itself: it can be deleted and derived again
/// from the audit store at any time, and a rebuild must reproduce it exactly. Delete policy: RETAIN (rebuilt, never purged).
/// </summary>
public sealed class BusinessActivityEntry : AuditedEntity
{
    public Guid AuditEventId { get; set; }

    public Guid? ProjectId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public Guid? ActorUserId { get; set; }

    /// <summary>The source domain the activity belongs to, e.g. <c>RISK</c> or <c>CHANGE_REQUEST</c>: the timeline's domain filter.</summary>
    public required string ActivityKind { get; set; }

    public required string SummaryAr { get; set; }

    public required string SummaryEn { get; set; }
}
