namespace PMPlatform.Application.Features.Suspension.Contracts.Events;

/// <summary>
/// The audit event types Suspension produces (TASK-062; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the producer's
/// unit of work. Every one has the request as subject and carries its type. Approval and activation are separate events of separate
/// transactions: <see cref="RequestApproved"/> changes no project; <see cref="RequestEffected"/> is the lifecycle transition, with
/// Project's own <c>Project.ProjectSuspended</c> or <c>Project.ProjectResumed</c> beside it. The list is appended to event-conventions.md §4.
/// </summary>
public static class SuspensionAuditEvents
{
    /// <summary>DATA_CHANGE: a request was raised, DRAFT (reason withheld).</summary>
    public const string RequestCreated = "Suspension.RequestCreated";

    /// <summary>DATA_CHANGE: a DRAFT or RETURNED request's fields changed (reason withheld).</summary>
    public const string RequestChanged = "Suspension.RequestChanged";

    /// <summary>DATA_CHANGE: a DRAFT never submitted was deleted.</summary>
    public const string RequestDeleted = "Suspension.RequestDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED, with the revision.</summary>
    public const string RequestSubmitted = "Suspension.RequestSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW, with the WF-11 run.</summary>
    public const string ReviewStarted = "Suspension.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → APPROVED, by WF-11's last approver. The business decision: no project changes.</summary>
    public const string RequestApproved = "Suspension.RequestApproved";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED, by WF-11's decider.</summary>
    public const string RequestReturned = "Suspension.RequestReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → REJECTED, by WF-11's decider. Final.</summary>
    public const string RequestRejected = "Suspension.RequestRejected";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED or RETURNED → WITHDRAWN by the requester, or UNDER_REVIEW → WITHDRAWN as its WF-11 run was. Final.</summary>
    public const string RequestWithdrawn = "Suspension.RequestWithdrawn";

    /// <summary>
    /// LIFECYCLE_TRANSITION: APPROVED → EFFECTED — the lifecycle activation — with the suspension period opened or ended, by the person
    /// who activated it or by WF-09's service principal on the effective date. Final.
    /// </summary>
    public const string RequestEffected = "Suspension.RequestEffected";

    /// <summary>AUTHORIZATION_DENIAL: review or activation refused to an external user (ADR-013: AHDA's).</summary>
    public const string AuthorityRefused = "Suspension.AuthorityRefused";

    /// <summary>DATA_CHANGE: a WF-11 outcome for a revision that is no longer under review, recorded and not applied (EV-5).</summary>
    public const string OutcomeIgnored = "Suspension.OutcomeIgnored";
}
