using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Milestone;

/// <summary>
/// One revision of an achievement claim for a shared <c>schedule.project_milestone</c> (ICD-04, TASK-050). WF-05 owns it: the
/// claim, its evidence (linked through DocumentManagement to this revision) and, once accepted, the Actual Achievement Date.
/// Revisions of a milestone are numbered from 1; a claim after a return, and a correction of an accepted achievement, are
/// each a new revision. Accepting one marks the milestone's previously accepted revision SUPERSEDED and leaves it otherwise
/// as it was. Delete policy: HARD_DRAFT.
/// </summary>
public sealed class MilestoneAchievement : AuditedEntity
{
    /// <summary>The shared milestone's identifier (M-4): WF-05 never holds a copy of it.</summary>
    public Guid ProjectMilestoneId { get; set; }

    public Guid ProjectId { get; set; }

    public int RevisionNo { get; set; } = 1;

    public MilestoneAchievementStatus Status { get; set; }

    public DateOnly ClaimedAchievementDate { get; set; }

    /// <summary>The WF-05-owned fact (ICD-04): set when WF-11 approves the revision, to the date it claimed.</summary>
    public DateOnly? AcceptedActualAchievementDate { get; set; }

    public NarrativeText? Narrative { get; set; }

    /// <summary>May be an entity Project Manager (ADR-013); acceptance stays with WF-05 through WF-11.</summary>
    public Guid? SubmittedByUserId { get; set; }

    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>Who ended the review: the final approver, the returner or rejecter, or the requester who withdrew.</summary>
    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The returning or rejecting approver's reason, as WF-11 recorded it; none on a withdrawal.</summary>
    public NarrativeText? ReturnReason { get; set; }

    /// <summary>The later revision whose acceptance superseded this one.</summary>
    public Guid? SupersededByAchievementId { get; set; }

    /// <summary>ADR-014: set when the achievement is recorded from a legacy intake's opening position (TASK-104).</summary>
    public Guid? ProjectIntakeId { get; set; }
}
