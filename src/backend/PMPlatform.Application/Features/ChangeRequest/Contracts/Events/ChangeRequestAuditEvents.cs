namespace PMPlatform.Application.Features.ChangeRequest.Contracts.Events;

/// <summary>
/// The audit event types ChangeRequest produces (TASK-060; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. Every one has the change request as subject. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ChangeRequestAuditEvents
{
    /// <summary>DATA_CHANGE: a change request was raised, DRAFT (title, justification and scope impact withheld).</summary>
    public const string ChangeRequestCreated = "ChangeRequest.ChangeRequestCreated";

    /// <summary>DATA_CHANGE: a DRAFT or RETURNED request's fields changed (narratives withheld).</summary>
    public const string ChangeRequestChanged = "ChangeRequest.ChangeRequestChanged";

    /// <summary>DATA_CHANGE: a DRAFT never submitted was deleted.</summary>
    public const string ChangeRequestDeleted = "ChangeRequest.ChangeRequestDeleted";

    /// <summary>LIFECYCLE_TRANSITION: DRAFT or RETURNED → SUBMITTED, with the revision.</summary>
    public const string ChangeRequestSubmitted = "ChangeRequest.ChangeRequestSubmitted";

    /// <summary>DATA_CHANGE: the authoritative materiality evaluation of a revision, with its bands, cumulative position and pinned version.</summary>
    public const string MaterialityEvaluated = "ChangeRequest.MaterialityEvaluated";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED → UNDER_REVIEW, with the WF-11 run and the band that routed it.</summary>
    public const string ReviewStarted = "ChangeRequest.ReviewStarted";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → APPROVED, by WF-11's last approver. Issues authorisations; changes no target.</summary>
    public const string ChangeRequestApproved = "ChangeRequest.ChangeRequestApproved";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → RETURNED, by WF-11's decider.</summary>
    public const string ChangeRequestReturned = "ChangeRequest.ChangeRequestReturned";

    /// <summary>LIFECYCLE_TRANSITION: UNDER_REVIEW → REJECTED, by WF-11's decider. Final.</summary>
    public const string ChangeRequestRejected = "ChangeRequest.ChangeRequestRejected";

    /// <summary>LIFECYCLE_TRANSITION: SUBMITTED or RETURNED → WITHDRAWN by the requester, or UNDER_REVIEW → WITHDRAWN as its WF-11 run was withdrawn. Final.</summary>
    public const string ChangeRequestWithdrawn = "ChangeRequest.ChangeRequestWithdrawn";

    /// <summary>LIFECYCLE_TRANSITION: the approval issued an authorisation, ISSUED, with its scope and pinned target.</summary>
    public const string ChangeAuthorizationIssued = "ChangeRequest.ChangeAuthorizationIssued";

    /// <summary>LIFECYCLE_TRANSITION: APPROVED → IMPLEMENTATION.</summary>
    public const string ImplementationStarted = "ChangeRequest.ImplementationStarted";

    /// <summary>
    /// LIFECYCLE_TRANSITION: an authorisation was applied — ISSUED → APPLIED — by the person who made the change in its target module,
    /// with the target and the record that applied it. The explicit application step, audited apart from the approval.
    /// </summary>
    public const string ChangeAuthorizationApplied = "ChangeRequest.ChangeAuthorizationApplied";

    /// <summary>LIFECYCLE_TRANSITION: IMPLEMENTATION → IMPLEMENTED, every authorisation applied.</summary>
    public const string ChangeRequestImplemented = "ChangeRequest.ChangeRequestImplemented";

    /// <summary>LIFECYCLE_TRANSITION: IMPLEMENTED → CLOSED.</summary>
    public const string ChangeRequestClosed = "ChangeRequest.ChangeRequestClosed";

    /// <summary>AUTHORIZATION_DENIAL: review or implementation refused to an external user (ADR-013: AHDA's).</summary>
    public const string AuthorityRefused = "ChangeRequest.AuthorityRefused";

    /// <summary>DATA_CHANGE: a WF-11 outcome for a revision that is no longer under review, recorded and not applied (EV-5).</summary>
    public const string OutcomeIgnored = "ChangeRequest.OutcomeIgnored";
}
