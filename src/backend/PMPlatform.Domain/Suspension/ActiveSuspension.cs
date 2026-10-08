using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Suspension;

/// <summary>
/// A period during which a project is SUSPENDED (WF-09, TASK-062): opened when a SUSPEND request is effected, ended when a RESUME
/// request is, or when the project is closed without resuming (WF-10's terminal path, TASK-063). At most one is open per project — a
/// partial unique index holds it — and an ended one is history, kept across repeated suspension cycles (BR-SUS-039). It records the
/// lifecycle only: no baseline, forecast or due date moves with it. Delete policy: RETAIN.
/// </summary>
public sealed class ActiveSuspension : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The SUSPEND request whose effect opened it.</summary>
    public Guid SuspensionRequestId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Null while it is open.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>Why it ended; set exactly when <see cref="EndedAt"/> is.</summary>
    public SuspensionEndReason? EndReason { get; set; }

    /// <summary>The RESUME request whose effect ended it; set exactly when it ended as <see cref="SuspensionEndReason.Resumed"/>.</summary>
    public Guid? ResumptionRequestId { get; set; }
}
