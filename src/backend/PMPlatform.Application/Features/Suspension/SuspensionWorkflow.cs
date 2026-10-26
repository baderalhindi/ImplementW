using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Suspension;

namespace PMPlatform.Application.Features.Suspension;

/// <summary>
/// A suspension or resumption request's state machine (TASK-062): Draft → Submitted → Under Review → Returned / Approved / Rejected,
/// Withdrawn, and Approved → Effected. WF-11's decision takes UNDER_REVIEW to APPROVED, RETURNED, REJECTED or WITHDRAWN; every other edge
/// is a command. APPROVED is entered only from UNDER_REVIEW and EFFECTED only from APPROVED, so a request is decided and effected in two
/// steps, never one (WF-09 P3, BR-SUS-006). Migration <c>TASK-062_GuardSuspensionHistory</c> refuses every other change in the database too.
/// </summary>
internal static class SuspensionWorkflow
{
    public static IReadOnlySet<(SuspensionRequestStatus From, SuspensionRequestStatus To)> Transitions { get; } = new HashSet<(SuspensionRequestStatus, SuspensionRequestStatus)>
    {
        (SuspensionRequestStatus.Draft, SuspensionRequestStatus.Submitted),          // submit
        (SuspensionRequestStatus.Returned, SuspensionRequestStatus.Submitted),       // submit, as the next revision
        (SuspensionRequestStatus.Submitted, SuspensionRequestStatus.UnderReview),    // start-review: WF-11 run started
        (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Approved),     // WF-11 approved: no project changes
        (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Returned),     // WF-11 returned
        (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Rejected),     // WF-11 rejected
        (SuspensionRequestStatus.UnderReview, SuspensionRequestStatus.Withdrawn),    // WF-11 run withdrawn by the requester
        (SuspensionRequestStatus.Submitted, SuspensionRequestStatus.Withdrawn),      // withdraw
        (SuspensionRequestStatus.Returned, SuspensionRequestStatus.Withdrawn),       // withdraw
        (SuspensionRequestStatus.Approved, SuspensionRequestStatus.Effected),        // activate: the project's lifecycle transition
    };

    /// <summary>Where <paramref name="command"/> takes a request in <paramref name="from"/>; null when the state machine has no such edge.</summary>
    public static SuspensionRequestStatus? TargetOf(SuspensionCommand command, SuspensionRequestStatus from) => (command, from) switch
    {
        (SuspensionCommand.Submit, SuspensionRequestStatus.Draft or SuspensionRequestStatus.Returned) => SuspensionRequestStatus.Submitted,
        (SuspensionCommand.Withdraw, SuspensionRequestStatus.Submitted or SuspensionRequestStatus.Returned) => SuspensionRequestStatus.Withdrawn,
        (SuspensionCommand.StartReview, SuspensionRequestStatus.Submitted) => SuspensionRequestStatus.UnderReview,
        (SuspensionCommand.Activate, SuspensionRequestStatus.Approved) => SuspensionRequestStatus.Effected,
        _ => null,
    };

    /// <summary>Where WF-11's decision takes a request UNDER_REVIEW.</summary>
    public static SuspensionRequestStatus OutcomeOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Approved => SuspensionRequestStatus.Approved,
        ApprovalOutcomeDecision.Returned => SuspensionRequestStatus.Returned,
        ApprovalOutcomeDecision.Rejected => SuspensionRequestStatus.Rejected,
        ApprovalOutcomeDecision.Withdrawn => SuspensionRequestStatus.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>Its fields change while it is with its requester: before its first submission, and when returned for correction.</summary>
    public static bool IsEditable(SuspensionRequestStatus status) => status is SuspensionRequestStatus.Draft or SuspensionRequestStatus.Returned;

    /// <summary>REJECTED, WITHDRAWN and EFFECTED change no more: reconsideration is a new request (BR-SUS-025).</summary>
    public static bool IsFinal(SuspensionRequestStatus status) =>
        status is SuspensionRequestStatus.Rejected or SuspensionRequestStatus.Withdrawn or SuspensionRequestStatus.Effected;
}

/// <summary>The commands that move a request along <see cref="SuspensionWorkflow"/>, one per edge family (api-conventions R-4).</summary>
internal enum SuspensionCommand
{
    Submit = 1,
    Withdraw = 2,
    StartReview = 3,
    Activate = 4,
}
