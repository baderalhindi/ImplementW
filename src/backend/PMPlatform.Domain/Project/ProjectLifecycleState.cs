namespace PMPlatform.Domain.Project;

/// <summary>
/// TASK-041 (Draft → Submitted → Under Review → Returned / Approved-Planned; Planned → Active), TASK-062
/// (Active ↔ Suspended), TASK-063 (Completed → Closed; Closed is terminal).
/// </summary>
public enum ProjectLifecycleState
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    ApprovedPlanned = 5,
    Active = 6,
    Suspended = 7,
    Completed = 8,
    Closed = 9,
}

/// <summary>Groups of <see cref="ProjectLifecycleState"/> that more than one module reads the same way.</summary>
public static class ProjectLifecycleStates
{
    /// <summary>A project still being registered: not yet the register's, so the portfolio figures leave it out (the 5 Nov 2026 review §3).</summary>
    public static IReadOnlyList<ProjectLifecycleState> Registering { get; } =
    [
        ProjectLifecycleState.Draft, ProjectLifecycleState.Submitted, ProjectLifecycleState.UnderReview, ProjectLifecycleState.Returned,
    ];
}
