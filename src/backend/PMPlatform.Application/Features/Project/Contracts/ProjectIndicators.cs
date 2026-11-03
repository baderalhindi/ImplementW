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
    IReadOnlyList<FollowUpReason> FollowUpReasons,
    EntityUpdateCompliance? EntityUpdates = null)
{
    public static ProjectIndicators None { get; } = new(null, null, null, null, []);
}

/// <summary>
/// Whether the delivering entity keeps to its updates (R3-3): calculated from the update requests sent to it — compliant when at least the
/// threshold's share was answered on time — unless RPMO recorded otherwise, which overrides it. <see cref="Calculated"/> is null with no
/// request counted yet.
/// </summary>
public sealed record EntityUpdateCompliance(int Counted, int OnTime, decimal? OnTimePercent, bool? Calculated, bool? Effective, bool Overridden);

/// <summary>
/// The follow-up thresholds in force: points behind plan from which a project is late and stalled (5 and 10 unless WORKFLOW_POLICY sets them),
/// and the share of update requests answered on time from which an entity is compliant (80% unless WORKFLOW_POLICY sets it).
/// </summary>
public sealed record OnPlanThresholds(decimal LatePoints, decimal StalledPoints, decimal CompliancePercent = 80m)
{
    /// <summary>The thresholds RPMO's register uses today (the 2 Nov 2026 review §2).</summary>
    public static OnPlanThresholds Default { get; } = new(5m, 10m);
}
