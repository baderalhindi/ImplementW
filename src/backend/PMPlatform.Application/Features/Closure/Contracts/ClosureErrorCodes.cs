namespace PMPlatform.Application.Features.Closure.Contracts;

/// <summary>The Closure module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ClosureErrorCodes
{
    /// <summary>
    /// 422: the project's state does not admit the case. A completion case is of an ACTIVE project (BR-CLO-002); a closure case is of a
    /// COMPLETED project, following its effected completion case, or — the terminal path — of a SUSPENDED project that will not resume
    /// (BR-CLO-003). Checked at raising, editing, evaluation, submission, review and activation.
    /// </summary>
    public const string ProjectNotEligible = "CLOSURE_PROJECT_NOT_ELIGIBLE";

    /// <summary>409: another case of the same kind for the project is not yet final — at most one open per project (CLO-CC-05, CLO-CC-06).</summary>
    public const string CaseAlreadyOpen = "CLOSURE_CASE_ALREADY_OPEN";

    /// <summary>409: the case is no longer a DRAFT or RETURNED: its fields are those it was submitted with. Only a DRAFT never submitted is deleted.</summary>
    public const string CaseNotEditable = "CLOSURE_CASE_NOT_EDITABLE";

    /// <summary>409: the draft has readiness records or obligations, which are kept: it is withdrawn rather than deleted.</summary>
    public const string CaseInUse = "CLOSURE_CASE_IN_USE";

    /// <summary>409: the case is under review; its originator withdraws the WF-11 run, which withdraws the case.</summary>
    public const string CaseUnderReview = "CLOSURE_CASE_UNDER_REVIEW";

    /// <summary>422: submitting without what review needs: a completion case's actual completion date and narrative, a closure case's narrative.</summary>
    public const string Incomplete = "CLOSURE_INCOMPLETE";

    /// <summary>422: an actual project completion date after today, or before the project was activated (WF-10 §8, BR-CLO-008).</summary>
    public const string CompletionDateInvalid = "CLOSURE_COMPLETION_DATE_INVALID";

    /// <summary>
    /// 422: the readiness evaluation is NOT_READY — at least one criterion failed and is not waived — so the case is not submitted, or not
    /// activated (WF-10 §6.3, BR-CLO-034). The failing criteria are the error's fields.
    /// </summary>
    public const string BlockerExists = "CLOSURE_BLOCKER_EXISTS";

    /// <summary>422: the criterion did not fail on the case's latest evaluation, or the case has none: there is nothing to waive.</summary>
    public const string CheckNotFailed = "CLOSURE_CHECK_NOT_FAILED";

    /// <summary>
    /// 422: the criterion is not waivable — a decision still to land, an open suspension or resumption request, an obligation without an
    /// owner or still open: each is settled where it lives, never by exception.
    /// </summary>
    public const string CheckNotWaivable = "CLOSURE_CHECK_NOT_WAIVABLE";

    /// <summary>422: an obligation's case is not one of its project's open or effected completion cases, or an open terminal closure case.</summary>
    public const string ObligationCaseInvalid = "CLOSURE_OBLIGATION_CASE_INVALID";

    /// <summary>422: an obligation's owner is not an active user.</summary>
    public const string ObligationOwnerInvalid = "CLOSURE_OBLIGATION_OWNER_INVALID";

    /// <summary>409: the obligation is SATISFIED, WAIVED or CANCELLED: settled, and changed no more.</summary>
    public const string ObligationSettled = "CLOSURE_OBLIGATION_SETTLED";
}
