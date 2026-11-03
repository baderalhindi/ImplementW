namespace PMPlatform.Application.Features.Project.Contracts;

/// <summary>
/// RPMO's on-plan indicator (مؤشر الالتزام بالخطة, the 2 Nov 2026 review §2): where actual progress stands against planned, in percentage
/// points. On track above the late threshold, late from it to just above the stalled one, stalled at the stalled one or worse.
/// </summary>
public enum OnPlanStatus
{
    OnTrack = 1,
    Late = 2,
    Stalled = 3,
}

/// <summary>Whether a project's next action is still to come or past its due date.</summary>
public enum ActionDueStatus
{
    Scheduled = 1,
    Overdue = 2,
}

/// <summary>Why a project is on the Follow-up list, in the order RPMO follows them up: stalled first, an open obstacle last.</summary>
public enum FollowUpReason
{
    Stalled = 1,
    Suspended = 2,
    Late = 3,
    ActionOverdue = 4,
    OpenObstacle = 5,
}

/// <summary>
/// The register's calculated columns (R3-3): the gap between actual and planned progress, in points, the on-plan indicator, the days to
/// the planned end, the next action's due status and the follow-up reasons, most urgent first. A value that cannot be calculated is null:
/// no gap without both percentages, none for a project not started or closed.
/// </summary>
public sealed record ProjectIndicators(
    decimal? GapPoints,
    OnPlanStatus? OnPlan,
    int? DaysRemaining,
    ActionDueStatus? ActionDue,
    IReadOnlyList<FollowUpReason> FollowUpReasons)
{
    public static ProjectIndicators None { get; } = new(null, null, null, null, []);
}

/// <summary>The on-plan thresholds in force, as positive points behind plan: 5 and 10 unless WORKFLOW_POLICY sets them.</summary>
public sealed record OnPlanThresholds(decimal LatePoints, decimal StalledPoints)
{
    /// <summary>The thresholds RPMO's register uses today (the 2 Nov 2026 review §2).</summary>
    public static OnPlanThresholds Default { get; } = new(5m, 10m);
}
