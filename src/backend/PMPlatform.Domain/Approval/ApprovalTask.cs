using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Approval;

/// <summary>
/// One stage assignment of an approval run and its decision; the run's history is its tasks in order (SCR-115).
/// A task is assigned to a role; <see cref="AssignedUserId"/> is the holder whose authority decided it, and
/// <see cref="ActingUserId"/> the person who acted, who differs only under a delegation. Delete policy: RETAIN.
/// </summary>
public sealed class ApprovalTask : AuditedEntity
{
    public Guid ApprovalInstanceId { get; set; }

    /// <summary>The stage, from the authority matrix row.</summary>
    public short SequenceNo { get; set; }

    public Guid AssignedRoleId { get; set; }

    public Guid? AssignedUserId { get; set; }

    public Guid? ActingUserId { get; set; }

    /// <summary>Set when the acting user decided under the assigned user's delegation.</summary>
    public Guid? ApprovalDelegationId { get; set; }

    public ApprovalTaskStatus Status { get; set; }

    public DateTimeOffset? DueAt { get; set; }

    /// <summary>When the decider's eligibility was checked again, at the moment of the decision.</summary>
    public DateTimeOffset? EligibilityRevalidatedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>Required on REJECTED and RETURNED; optional on an escalation.</summary>
    public NarrativeText? DecisionReason { get; set; }

    /// <summary>The task that took this one's place when it was escalated.</summary>
    public Guid? EscalatedToTaskId { get; set; }
}
