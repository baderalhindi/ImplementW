namespace PMPlatform.Application.Features.Milestone.Contracts;

/// <summary>
/// How an achievement revision names itself to WF-11 (M-8; ADR-003 §8.2 edge 25): the subject module and type, and the
/// APPROVAL_AUTHORITY <c>subject_type_code</c> AHDA configures the acceptance route under.
/// </summary>
public static class MilestoneApprovalRouting
{
    public const string SubjectModule = "Milestone";

    public const string SubjectType = "MilestoneAchievement";

    /// <summary>The route of an achievement claim: SUBMITTED → ACCEPTED or RETURNED.</summary>
    public const string AchievementRoutingKey = "MILESTONE_ACHIEVEMENT";
}
