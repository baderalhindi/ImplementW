using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Suspension;

/// <summary>
/// A request to suspend an ACTIVE project or resume a SUSPENDED one (WF-09, TASK-062). It is raised by the project's people — an entity
/// Project Manager included (ADR-013) — reviewed by AHDA and decided through WF-11. Approval changes no project: the request is effected
/// later, as its own step, which moves the project and opens or ends its <see cref="ActiveSuspension"/> together. Delete policy:
/// HARD_DRAFT — a request never submitted may be deleted; one submitted is withdrawn, rejected or effected, never removed.
/// </summary>
public sealed class SuspensionRequest : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public SuspensionRequestType RequestType { get; set; }

    public SuspensionRequestStatus Status { get; set; }

    /// <summary>The business revision a WF-11 run reviews; a RETURNED request is resubmitted as the next one.</summary>
    public int RevisionNo { get; set; } = 1;

    /// <summary>Why the project should stop or may resume, stored as entered (ADR-012).</summary>
    public required NarrativeText Reason { get; set; }

    /// <summary>The originator: the WF-11 run's requester, so WF-11 keeps them from approving their own request.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>When the request was last submitted; null only while it is a DRAFT never submitted.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>The business date from which the request takes effect; an approved request is effected on or after it.</summary>
    public DateOnly? RequestedEffectiveDate { get; set; }

    /// <summary>SUSPEND only: when the project is expected to resume. Planning information; nothing resumes on it (WF-09 BR-SUS-031).</summary>
    public DateOnly? PlannedResumptionDate { get; set; }

    /// <summary>When the project's lifecycle transition ran: a separate, separately audited event from the approval.</summary>
    public DateTimeOffset? EffectedAt { get; set; }
}
