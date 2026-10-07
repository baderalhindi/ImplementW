using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// A change request's state machine (TASK-060): Draft → Submitted → Under Review → Returned / Approved / Rejected → Implementation →
/// Implemented → Closed, and Withdrawn. WF-11's decision takes UNDER_REVIEW to APPROVED, RETURNED, REJECTED or WITHDRAWN; every other
/// edge is a command. No edge enters APPROVED but from UNDER_REVIEW, and none enters IMPLEMENTED but from IMPLEMENTATION, which only
/// APPROVED enters. Migration <c>TASK-060_GuardChangeRequestHistory</c> refuses every other change of status in the database too.
/// </summary>
internal static class ChangeRequestWorkflow
{
    public static IReadOnlySet<(ChangeRequestStatus From, ChangeRequestStatus To)> Transitions { get; } = new HashSet<(ChangeRequestStatus, ChangeRequestStatus)>
    {
        (ChangeRequestStatus.Draft, ChangeRequestStatus.Submitted),              // submit
        (ChangeRequestStatus.Returned, ChangeRequestStatus.Submitted),           // submit, as the next revision
        (ChangeRequestStatus.Submitted, ChangeRequestStatus.UnderReview),        // start-review: materiality recorded, WF-11 run started
        (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Approved),         // WF-11 approved: authorisations issued
        (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Returned),         // WF-11 returned
        (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Rejected),         // WF-11 rejected
        (ChangeRequestStatus.UnderReview, ChangeRequestStatus.Withdrawn),        // WF-11 run withdrawn by the requester
        (ChangeRequestStatus.Submitted, ChangeRequestStatus.Withdrawn),          // withdraw
        (ChangeRequestStatus.Returned, ChangeRequestStatus.Withdrawn),           // withdraw
        (ChangeRequestStatus.Approved, ChangeRequestStatus.Implementation),      // start-implementation
        (ChangeRequestStatus.Implementation, ChangeRequestStatus.Implemented),   // mark-implemented: every authorisation applied
        (ChangeRequestStatus.Implemented, ChangeRequestStatus.Closed),           // close
    };

    /// <summary>Where <paramref name="command"/> takes a request in <paramref name="from"/>; null when the state machine has no such edge.</summary>
    public static ChangeRequestStatus? TargetOf(ChangeRequestCommand command, ChangeRequestStatus from) => (command, from) switch
    {
        (ChangeRequestCommand.Submit, ChangeRequestStatus.Draft or ChangeRequestStatus.Returned) => ChangeRequestStatus.Submitted,
        (ChangeRequestCommand.Withdraw, ChangeRequestStatus.Submitted or ChangeRequestStatus.Returned) => ChangeRequestStatus.Withdrawn,
        (ChangeRequestCommand.StartReview, ChangeRequestStatus.Submitted) => ChangeRequestStatus.UnderReview,
        (ChangeRequestCommand.StartImplementation, ChangeRequestStatus.Approved) => ChangeRequestStatus.Implementation,
        (ChangeRequestCommand.MarkImplemented, ChangeRequestStatus.Implementation) => ChangeRequestStatus.Implemented,
        (ChangeRequestCommand.Close, ChangeRequestStatus.Implemented) => ChangeRequestStatus.Closed,
        _ => null,
    };

    /// <summary>Where WF-11's decision takes a request UNDER_REVIEW.</summary>
    public static ChangeRequestStatus OutcomeOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Approved => ChangeRequestStatus.Approved,
        ApprovalOutcomeDecision.Returned => ChangeRequestStatus.Returned,
        ApprovalOutcomeDecision.Rejected => ChangeRequestStatus.Rejected,
        ApprovalOutcomeDecision.Withdrawn => ChangeRequestStatus.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>Its fields change while it is with its requester: before its first submission, and when returned for correction.</summary>
    public static bool IsEditable(ChangeRequestStatus status) => status is ChangeRequestStatus.Draft or ChangeRequestStatus.Returned;

    /// <summary>Its materiality is previewed, not yet recorded, until its review starts.</summary>
    public static bool IsBeforeReview(ChangeRequestStatus status) => IsEditable(status) || status == ChangeRequestStatus.Submitted;

    /// <summary>REJECTED, WITHDRAWN and CLOSED change no more: a later proposal is a new request (WF-08 BR-CHG-015).</summary>
    public static bool IsFinal(ChangeRequestStatus status) => status is ChangeRequestStatus.Rejected or ChangeRequestStatus.Withdrawn or ChangeRequestStatus.Closed;
}

/// <summary>The commands that move a request along <see cref="ChangeRequestWorkflow"/>, one per edge family (api-conventions R-4).</summary>
internal enum ChangeRequestCommand
{
    Submit = 1,
    Withdraw = 2,
    StartReview = 3,
    StartImplementation = 4,
    MarkImplemented = 5,
    Close = 6,
}
