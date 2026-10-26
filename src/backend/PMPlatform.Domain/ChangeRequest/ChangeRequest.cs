using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ChangeRequest;

/// <summary>
/// A formal request to change a project's governed commitments (WF-08, TASK-060). It is raised and submitted by the project's people —
/// an entity Project Manager included (ADR-013) — and evaluated, reviewed, approved and implemented by AHDA. Approval issues
/// <see cref="ChangeAuthorization"/>s and changes nothing else: each is applied by the module that owns its target. Delete policy:
/// HARD_DRAFT — a request never submitted may be deleted; one submitted is withdrawn, rejected or closed, never removed.
/// </summary>
public sealed class ChangeRequest : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public required NarrativeText Title { get; set; }

    public required NarrativeText Justification { get; set; }

    public ChangeType ChangeType { get; set; }

    public ChangeRequestStatus Status { get; set; }

    /// <summary>The business revision a WF-11 run reviews; a RETURNED request is resubmitted as the next one.</summary>
    public int RevisionNo { get; set; } = 1;

    /// <summary>The originator: the WF-11 run's requester, so WF-11 keeps them from approving their own change.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>When the request was last submitted; null only while it is a DRAFT never submitted.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>The cost the change adds to the Approved Budget; negative for a reduction. A cost dimension and a COMMITMENT_CHANGE target.</summary>
    public Money? CostImpactSar { get; set; }

    /// <summary>The calendar days the change moves the Approved Baseline's finish; negative to bring it forward. A REBASELINE target.</summary>
    public int? ScheduleImpactDays { get; set; }

    public NarrativeText? ScopeImpact { get; set; }

    /// <summary>A change to a contractual obligation is always band 3 (TASK-106).</summary>
    public bool IsContractualObligation { get; set; }

    /// <summary>GOVERNANCE_PROFILE changes: the profile asked for (TASK-105).</summary>
    public Guid? RequestedGovernanceProfileItemId { get; set; }

    public DateTimeOffset? ImplementedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }
}
