using PMPlatform.Domain.Schedule;

namespace PMPlatform.Application.Features.Schedule;

/// <summary>
/// The baseline's edges (TASK-046). An APPROVED candidate is born DRAFT; submission sends it to WF-11 (SUBMITTED), or
/// activates it at once where its governance profile requires no approval (ADR-015). WF-11's outcome activates it, returns it
/// for a new revision, rejects it, or records its withdrawal. Activating one supersedes the ACTIVE one in the same
/// transaction. A DECLARED baseline is born ACTIVE (ADR-014). Migration <c>TASK-046_GuardScheduleHistory</c> refuses every
/// other change in the database too.
/// </summary>
internal static class BaselineWorkflow
{
    public static IReadOnlySet<(ProjectBaselineStatus From, ProjectBaselineStatus To)> Transitions { get; } = new HashSet<(ProjectBaselineStatus, ProjectBaselineStatus)>
    {
        (ProjectBaselineStatus.Draft, ProjectBaselineStatus.Submitted),      // submit, approval required
        (ProjectBaselineStatus.Returned, ProjectBaselineStatus.Submitted),   // resubmit as revision + 1
        (ProjectBaselineStatus.Draft, ProjectBaselineStatus.Active),         // submit, no approval required (ADR-015)
        (ProjectBaselineStatus.Returned, ProjectBaselineStatus.Active),      // resubmit, no approval required now
        (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Active),     // WF-11 APPROVED
        (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Returned),   // WF-11 RETURNED, or approved but no longer activatable
        (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Rejected),   // WF-11 REJECTED
        (ProjectBaselineStatus.Submitted, ProjectBaselineStatus.Withdrawn),  // WF-11 WITHDRAWN by its requester
        (ProjectBaselineStatus.Active, ProjectBaselineStatus.Superseded),    // another baseline activated
    };

    public static bool Allows(ProjectBaselineStatus from, ProjectBaselineStatus to) => Transitions.Contains((from, to));

    /// <summary>A candidate still on its way: while one exists, the project gets no other.</summary>
    public static bool IsOpen(ProjectBaselineStatus status) =>
        status is ProjectBaselineStatus.Draft or ProjectBaselineStatus.Submitted or ProjectBaselineStatus.UnderReview or ProjectBaselineStatus.Returned;
}
