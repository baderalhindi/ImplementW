using PMPlatform.Domain.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>
/// The WF-01 state machine as TASK-041 builds it (Blueprint Sections 5, 9; ICD-02), with WF-09's two edges (TASK-062) and WF-10's three
/// (TASK-063). Each edge has exactly one cause: a command of <see cref="ProjectService"/>, the review's WF-11 outcome, WF-09 effecting an
/// approved suspension or resumption request through <see cref="ProjectSuspensionCommands"/> (ADR-003 §8.2 edge 7), or WF-10 effecting an
/// approved completion or closure case through <see cref="ProjectCloseoutCommands"/> (edge 8). There is no edge into APPROVED_PLANNED but
/// from UNDER_REVIEW, so no project skips review: no fast-track rule is authorised (PTBC-002, Open). COMPLETED is entered only from ACTIVE
/// by an effected completion case — never by progress, tasks or dates (WF-10 BR-CLO-009) — and CLOSED only from COMPLETED, or from
/// SUSPENDED on WF-10's terminal path, by an effected closure case. CLOSED is terminal: no edge leaves it. Migration
/// <c>TASK-063_AddProjectCloseoutEdges</c> refuses every other change in the database too.
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
        (ProjectLifecycleState.Active, ProjectLifecycleState.Completed),           // WF-10 effects an approved completion case
        (ProjectLifecycleState.Completed, ProjectLifecycleState.Closed),           // WF-10 effects an approved closure case
        (ProjectLifecycleState.Suspended, ProjectLifecycleState.Closed),           // WF-10 effects a terminal closure case: closed, never completed
    };

    public static bool Allows(ProjectLifecycleState from, ProjectLifecycleState to) => Transitions.Contains((from, to));

    /// <summary>The states whose registration fields the registrant still writes.</summary>
    public static bool IsEditable(ProjectLifecycleState state) => state is ProjectLifecycleState.Draft or ProjectLifecycleState.Returned;
}
