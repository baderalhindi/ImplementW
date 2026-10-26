namespace PMPlatform.Application.Features.Approval.Contracts;

/// <summary>The Approval module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ApprovalErrorCodes
{
    /// <summary>409: a run of this subject is still PENDING; it is decided or withdrawn before another starts.</summary>
    public const string AlreadyPending = "APPROVAL_ALREADY_PENDING";

    /// <summary>409: another decision on the same run was saved first; read the run again.</summary>
    public const string ConcurrentDecision = "APPROVAL_CONCURRENT_DECISION";

    /// <summary>422: the revision is not newer than the subject's last run. A decided run is never reopened (TASK-035).</summary>
    public const string RevisionStale = "APPROVAL_REVISION_STALE";

    /// <summary>422: rejecting or returning needs a reason (TASK-036).</summary>
    public const string ReasonRequired = "APPROVAL_REASON_REQUIRED";

    /// <summary>422: the task is not overdue, is not in the current stage, or already took an escalation's place.</summary>
    public const string EscalationNotAllowed = "APPROVAL_ESCALATION_NOT_ALLOWED";

    /// <summary>422: the delegate is the delegator, or is not an active internal user (ADR-013).</summary>
    public const string DelegateInvalid = "APPROVAL_DELEGATE_INVALID";

    /// <summary>422: the period starts in the past or does not end after it starts.</summary>
    public const string DelegationPeriodInvalid = "APPROVAL_DELEGATION_PERIOD_INVALID";

    /// <summary>422: the requester is not an active user, or would approve their own request.</summary>
    public const string RequesterInvalid = "APPROVAL_REQUESTER_INVALID";
}
