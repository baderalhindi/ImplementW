using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// The WF-01 state machine as TASK-041 builds it (Blueprint Sections 5, 9; ICD-02), with WF-09's two edges (TASK-062). Each edge
/// has exactly one cause: a command of <see cref="ProjectService"/>, the review's WF-11 outcome, or WF-09 effecting an approved
/// suspension or resumption request through <see cref="ProjectSuspensionCommands"/> (ADR-003 §8.2 edge 7). There is no edge into
/// APPROVED_PLANNED but from UNDER_REVIEW, so no project skips review: no fast-track rule is authorised (PTBC-002, Open). ACTIVE is
/// entered by the activation command, or from SUSPENDED by a resumption, and left only for SUSPENDED; TASK-063 adds its own edges.
/// Migration <c>TASK-062_AddProjectSuspensionEdges</c> refuses every other change in the database too.
/// </summary>
internal static class ProjectLifecycle
{
    public static IReadOnlySet<(ProjectLifecycleState From, ProjectLifecycleState To)> Transitions { get; } = new HashSet<(ProjectLifecycleState, ProjectLifecycleState)>
    {
        (ProjectLifecycleState.Draft, ProjectLifecycleState.Submitted),            // submit
        (ProjectLifecycleState.Submitted, ProjectLifecycleState.Draft),            // withdraw
        (ProjectLifecycleState.Submitted, ProjectLifecycleState.UnderReview),      // start-review, with the WF-11 run
        (ProjectLifecycleState.UnderReview, ProjectLifecycleState.Returned),       // outcome RETURNED, REJECTED or WITHDRAWN
        (ProjectLifecycleState.UnderReview, ProjectLifecycleState.ApprovedPlanned), // outcome APPROVED; the Formal Project ID is issued
        (ProjectLifecycleState.Returned, ProjectLifecycleState.Submitted),         // submit, as revision + 1
        (ProjectLifecycleState.ApprovedPlanned, ProjectLifecycleState.Active),     // activate
        (ProjectLifecycleState.Active, ProjectLifecycleState.Suspended),           // WF-09 effects an approved suspension request
        (ProjectLifecycleState.Suspended, ProjectLifecycleState.Active),           // WF-09 effects an approved resumption request
    };

    public static bool Allows(ProjectLifecycleState from, ProjectLifecycleState to) => Transitions.Contains((from, to));

    /// <summary>The states whose registration fields the registrant still writes.</summary>
    public static bool IsEditable(ProjectLifecycleState state) => state is ProjectLifecycleState.Draft or ProjectLifecycleState.Returned;
}
