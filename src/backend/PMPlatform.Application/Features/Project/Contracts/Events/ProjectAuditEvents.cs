namespace PMPlatform.Application.Features.Project.Contracts.Events;

/// <summary>
/// The audit event types Project produces (TASK-041; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ProjectAuditEvents
{
    /// <summary>DATA_CHANGE: a DRAFT was created.</summary>
    public const string ProjectCreated = "Project.ProjectCreated";

    /// <summary>DATA_CHANGE: registration fields of a DRAFT or RETURNED project.</summary>
    public const string ProjectChanged = "Project.ProjectChanged";

    /// <summary>DATA_CHANGE: a DRAFT was deleted (HARD_DRAFT).</summary>
    public const string ProjectDeleted = "Project.ProjectDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED.</summary>
    public const string ProjectSubmitted = "Project.ProjectSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → DRAFT.</summary>
    public const string SubmissionWithdrawn = "Project.SubmissionWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW; the WF-11 run started.</summary>
    public const string ReviewStarted = "Project.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED, on a RETURNED, REJECTED or WITHDRAWN outcome.</summary>
    public const string RegistrationReturned = "Project.RegistrationReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → APPROVED_PLANNED; the Formal Project ID was issued.</summary>
    public const string RegistrationApproved = "Project.RegistrationApproved";

    /// <summary>LIFECYCLE_TRANSITION: APPROVED_PLANNED → ACTIVE, by the activation command.</summary>
    public const string ProjectActivated = "Project.ProjectActivated";

    /// <summary>LIFECYCLE_TRANSITION: ACTIVE → SUSPENDED, as WF-09 effects an approved suspension request (TASK-062, edge 7).</summary>
    public const string ProjectSuspended = "Project.ProjectSuspended";

    /// <summary>LIFECYCLE_TRANSITION: SUSPENDED → ACTIVE, as WF-09 effects an approved resumption request (TASK-062, edge 7). No baseline changes.</summary>
    public const string ProjectResumed = "Project.ProjectResumed";

    /// <summary>LIFECYCLE_TRANSITION, FAILED: an approval outcome for a revision or state the project is no longer in (EV-5).</summary>
    public const string ApprovalOutcomeIgnored = "Project.ApprovalOutcomeIgnored";

    /// <summary>AUTHORIZATION_DENIAL: an external user asked for an AHDA lifecycle gate (ADR-013).</summary>
    public const string LifecycleGateRefused = "Project.LifecycleGateRefused";
}
