using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>
/// An achievement revision's edges (TASK-050). It is born DRAFT and submitted to WF-11; WF-11's outcome accepts or returns it;
/// an ACCEPTED revision is superseded when a later revision of its milestone is accepted. No edge leaves RETURNED or
/// SUPERSEDED, and none edits an ACCEPTED claim: a correction is a new revision. Migration
/// <c>TASK-050_GuardMilestoneAchievement</c> refuses every other change in the database too.
/// </summary>
internal static class MilestoneAchievementWorkflow
{
    public static IReadOnlySet<(MilestoneAchievementStatus From, MilestoneAchievementStatus To)> Transitions { get; } = new HashSet<(MilestoneAchievementStatus, MilestoneAchievementStatus)>
    {
        (MilestoneAchievementStatus.Draft, MilestoneAchievementStatus.Submitted),      // submit to WF-11
        (MilestoneAchievementStatus.Submitted, MilestoneAchievementStatus.Accepted),   // WF-11 APPROVED
        (MilestoneAchievementStatus.Submitted, MilestoneAchievementStatus.Returned),   // WF-11 RETURNED, REJECTED or WITHDRAWN; or the milestone was cancelled
        (MilestoneAchievementStatus.Accepted, MilestoneAchievementStatus.Superseded),  // a later revision accepted
    };

    public static bool Allows(MilestoneAchievementStatus from, MilestoneAchievementStatus to) => Transitions.Contains((from, to));

    /// <summary>A revision still on its way: while one exists, the milestone gets no other.</summary>
    public static bool IsOpen(MilestoneAchievementStatus status) => status is MilestoneAchievementStatus.Draft or MilestoneAchievementStatus.Submitted;

    /// <summary>
    /// Where WF-11's decision leaves a SUBMITTED revision. Only an approval accepts. A return, a rejection and a withdrawal all
    /// send the claim back to its claimant, who may claim again in a new revision: the ERD's value set has no REJECTED or
    /// WITHDRAWN, and the decision itself is kept on the audit event and on WF-11's run.
    /// </summary>
    public static MilestoneAchievementStatus OutcomeOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Approved => MilestoneAchievementStatus.Accepted,
        ApprovalOutcomeDecision.Returned or ApprovalOutcomeDecision.Rejected or ApprovalOutcomeDecision.Withdrawn => MilestoneAchievementStatus.Returned,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };
}
