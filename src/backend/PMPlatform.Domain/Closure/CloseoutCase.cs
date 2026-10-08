using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Closure;

/// <summary>
/// What a completion case and a closure case share (WF-10, TASK-063): a request about one project, raised by its people — an entity
/// Project Manager included (ADR-013) — reviewed by AHDA and decided through WF-11. Approval changes no project: the case is effected
/// later, as its own step, which moves the project. Delete policy: HARD_DRAFT — a case never submitted may be deleted; one submitted is
/// withdrawn, rejected or effected, never removed.
/// </summary>
public abstract class CloseoutCase : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public CloseoutCaseStatus Status { get; set; }

    /// <summary>The business revision a WF-11 run reviews; a RETURNED case is resubmitted as the next one.</summary>
    public int RevisionNo { get; set; } = 1;

    /// <summary>The originator: the WF-11 run's requester, so WF-11 keeps them from approving their own case.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>When the case was last submitted; null only while it is a DRAFT never submitted.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>When the project's lifecycle transition ran: a separate, separately audited event from the approval.</summary>
    public DateTimeOffset? EffectedAt { get; set; }
}
