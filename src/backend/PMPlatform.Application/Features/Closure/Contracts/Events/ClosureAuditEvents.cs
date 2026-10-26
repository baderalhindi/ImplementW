namespace PMPlatform.Application.Features.Closure.Contracts.Events;

/// <summary>
/// The audit event types Closure produces (TASK-063; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the producer's unit
/// of work. A case's events have the case as subject (CompletionCase or ClosureCase); an obligation's, the obligation. Approval and
/// activation are separate events of separate transactions: <see cref="CaseApproved"/> changes no project; <see cref="CaseEffected"/> is
/// the lifecycle transition, with Project's own <c>Project.ProjectCompleted</c> or <c>Project.ProjectClosed</c> beside it. The list is
/// appended to event-conventions.md §4.
/// </summary>
public static class ClosureAuditEvents
{
    /// <summary>DATA_CHANGE: a case was raised, DRAFT (narrative withheld).</summary>
    public const string CaseCreated = "Closure.CaseCreated";

    /// <summary>DATA_CHANGE: a DRAFT or RETURNED case's fields changed (narrative withheld).</summary>
    public const string CaseChanged = "Closure.CaseChanged";

    /// <summary>DATA_CHANGE: a DRAFT never submitted was deleted.</summary>
    public const string CaseDeleted = "Closure.CaseDeleted";

    /// <summary>DATA_CHANGE: the case's readiness was evaluated, with the roll-up.</summary>
    public const string ReadinessEvaluated = "Closure.ReadinessEvaluated";

    /// <summary>DATA_CHANGE: a failed criterion was accepted as an exception (reason withheld).</summary>
    public const string CheckWaived = "Closure.CheckWaived";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED, with the revision and its readiness.</summary>
    public const string CaseSubmitted = "Closure.CaseSubmitted";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW, with the WF-11 run.</summary>
    public const string ReviewStarted = "Closure.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → APPROVED, by WF-11's last approver. The business decision: no project changes.</summary>
    public const string CaseApproved = "Closure.CaseApproved";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED, by WF-11's decider.</summary>
    public const string CaseReturned = "Closure.CaseReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → REJECTED, by WF-11's decider. Final.</summary>
    public const string CaseRejected = "Closure.CaseRejected";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED or RETURNED → WITHDRAWN by the requester, or UNDER_REVIEW → WITHDRAWN as its WF-11 run was. Final.</summary>
    public const string CaseWithdrawn = "Closure.CaseWithdrawn";

    /// <summary>
    /// LIFECYCLE_TRANSITION: APPROVED → EFFECTED — the lifecycle activation — by the person who activated it or by WF-10's service
    /// principal. Final.
    /// </summary>
    public const string CaseEffected = "Closure.CaseEffected";

    /// <summary>AUTHORIZATION_DENIAL: review, waiver or activation refused to an external user (ADR-013: AHDA's).</summary>
    public const string AuthorityRefused = "Closure.AuthorityRefused";

    /// <summary>DATA_CHANGE: a WF-11 outcome for a revision that is no longer under review, recorded and not applied (EV-5).</summary>
    public const string OutcomeIgnored = "Closure.OutcomeIgnored";

    /// <summary>DATA_CHANGE: a post-project obligation was recorded (title and description withheld).</summary>
    public const string ObligationCreated = "Closure.ObligationCreated";

    /// <summary>DATA_CHANGE: an open obligation's fields changed (title and description withheld).</summary>
    public const string ObligationChanged = "Closure.ObligationChanged";

    /// <summary>LIFECYCLE_TRANSITION: an obligation moved — started, satisfied, cancelled or waived.</summary>
    public const string ObligationTransitioned = "Closure.ObligationTransitioned";
}
