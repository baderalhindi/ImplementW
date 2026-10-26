using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Domain.Closure;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// A completion or closure case's state machine (TASK-063; WF-10 §4.1, §4.2, Appendix A): Draft → Submitted → Under Review → Returned /
/// Approved / Rejected, Withdrawn, and Approved → Effected. A draft is withdrawn rather than deleted once it has readiness records or
/// obligations, which are kept. WF-11's decision takes UNDER_REVIEW to APPROVED, RETURNED, REJECTED or
/// WITHDRAWN; every other edge is a command. APPROVED is entered only from UNDER_REVIEW and EFFECTED only from APPROVED, so a case is
/// decided and effected in two steps, never one (WF-10 P3, BR-CLO-004, BR-CLO-005). Migration <c>TASK-063_GuardCloseoutHistory</c> refuses
/// every other change in the database too.
/// </summary>
internal static class CloseoutWorkflow
{
    public static IReadOnlySet<(CloseoutCaseStatus From, CloseoutCaseStatus To)> Transitions { get; } = new HashSet<(CloseoutCaseStatus, CloseoutCaseStatus)>
    {
        (CloseoutCaseStatus.Draft, CloseoutCaseStatus.Submitted),          // submit
        (CloseoutCaseStatus.Returned, CloseoutCaseStatus.Submitted),       // submit, as the next revision
        (CloseoutCaseStatus.Submitted, CloseoutCaseStatus.UnderReview),    // start-review: WF-11 run started
        (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Approved),     // WF-11 approved: no project changes
        (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Returned),     // WF-11 returned
        (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Rejected),     // WF-11 rejected
        (CloseoutCaseStatus.UnderReview, CloseoutCaseStatus.Withdrawn),    // WF-11 run withdrawn by the requester
        (CloseoutCaseStatus.Draft, CloseoutCaseStatus.Withdrawn),          // withdraw (WF-10 §4.1): a draft with readiness or obligations
        (CloseoutCaseStatus.Submitted, CloseoutCaseStatus.Withdrawn),      // withdraw
        (CloseoutCaseStatus.Returned, CloseoutCaseStatus.Withdrawn),       // withdraw
        (CloseoutCaseStatus.Approved, CloseoutCaseStatus.Effected),        // activate: the project's lifecycle transition
    };

    /// <summary>Where <paramref name="command"/> takes a case in <paramref name="from"/>; null when the state machine has no such edge.</summary>
    public static CloseoutCaseStatus? TargetOf(CloseoutCommand command, CloseoutCaseStatus from) => (command, from) switch
    {
        (CloseoutCommand.Submit, CloseoutCaseStatus.Draft or CloseoutCaseStatus.Returned) => CloseoutCaseStatus.Submitted,
        (CloseoutCommand.Withdraw, CloseoutCaseStatus.Draft or CloseoutCaseStatus.Submitted or CloseoutCaseStatus.Returned) => CloseoutCaseStatus.Withdrawn,
        (CloseoutCommand.StartReview, CloseoutCaseStatus.Submitted) => CloseoutCaseStatus.UnderReview,
        (CloseoutCommand.Activate, CloseoutCaseStatus.Approved) => CloseoutCaseStatus.Effected,
        (CloseoutCommand.EvaluateReadiness or CloseoutCommand.WaiveCheck, CloseoutCaseStatus.Draft or CloseoutCaseStatus.Returned) => from,
        _ => null,
    };

    /// <summary>Where WF-11's decision takes a case UNDER_REVIEW.</summary>
    public static CloseoutCaseStatus OutcomeOf(ApprovalOutcomeDecision decision) => decision switch
    {
        ApprovalOutcomeDecision.Approved => CloseoutCaseStatus.Approved,
        ApprovalOutcomeDecision.Returned => CloseoutCaseStatus.Returned,
        ApprovalOutcomeDecision.Rejected => CloseoutCaseStatus.Rejected,
        ApprovalOutcomeDecision.Withdrawn => CloseoutCaseStatus.Withdrawn,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision, "Unknown decision."),
    };

    /// <summary>Its fields and readiness change while it is with its requester: before its first submission, and when returned for correction.</summary>
    public static bool IsEditable(CloseoutCaseStatus status) => status is CloseoutCaseStatus.Draft or CloseoutCaseStatus.Returned;

    /// <summary>REJECTED, WITHDRAWN and EFFECTED change no more: reconsideration is a new case (WF-10 §4.1).</summary>
    public static bool IsFinal(CloseoutCaseStatus status) =>
        status is CloseoutCaseStatus.Rejected or CloseoutCaseStatus.Withdrawn or CloseoutCaseStatus.Effected;
}

/// <summary>The commands on a case, one per edge family (api-conventions R-4); evaluation and waiver keep its status.</summary>
internal enum CloseoutCommand
{
    EvaluateReadiness = 1,
    WaiveCheck = 2,
    Submit = 3,
    Withdraw = 4,
    StartReview = 5,
    Activate = 6,
}
