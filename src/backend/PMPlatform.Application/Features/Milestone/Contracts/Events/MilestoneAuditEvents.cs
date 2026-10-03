namespace PMPlatform.Application.Features.Milestone.Contracts.Events;

/// <summary>
/// The audit event types Milestone produces (TASK-050; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class MilestoneAuditEvents
{
    /// <summary>DATA_CHANGE: a DRAFT revision was opened, with whether it corrects an accepted achievement.</summary>
    public const string AchievementStarted = "Milestone.AchievementStarted";

    /// <summary>DATA_CHANGE: a DRAFT's claim changed (the narrative withheld).</summary>
    public const string AchievementChanged = "Milestone.AchievementChanged";

    /// <summary>DATA_CHANGE: a DRAFT was deleted.</summary>
    public const string AchievementDeleted = "Milestone.AchievementDeleted";

    /// <summary>DATA_CHANGE: evidence was attached to a DRAFT.</summary>
    public const string EvidenceAttached = "Milestone.EvidenceAttached";

    /// <summary>DATA_CHANGE: a DRAFT's evidence was withdrawn.</summary>
    public const string EvidenceWithdrawn = "Milestone.EvidenceWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT → SUBMITTED, with the WF-11 run and the EVIDENCE_POLICY version checked.</summary>
    public const string AchievementSubmitted = "Milestone.AchievementSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → ACCEPTED, with the accepted date and the revision it superseded.</summary>
    public const string AchievementAccepted = "Milestone.AchievementAccepted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → RETURNED, with WF-11's decision (RETURNED, REJECTED or WITHDRAWN) or why an approval could not be applied.</summary>
    public const string AchievementReturned = "Milestone.AchievementReturned";

    /// <summary>LIFECYCLE_TRANSITION: ACCEPTED → SUPERSEDED, with the revision that superseded it.</summary>
    public const string AchievementSuperseded = "Milestone.AchievementSuperseded";

    /// <summary>LIFECYCLE_TRANSITION, FAILED: a WF-11 outcome for a revision that is no longer under review (EV-5).</summary>
    public const string ApprovalOutcomeIgnored = "Milestone.ApprovalOutcomeIgnored";
}
