namespace PMPlatform.Application.Features.Approval.Contracts.Events;

/// <summary>
/// The audit event types Approval produces (TASK-035; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ApprovalAuditEvents
{
    /// <summary>LIFECYCLE_TRANSITION: a run started.</summary>
    public const string ApprovalRequested = "Approval.ApprovalRequested";

    /// <summary>APPROVAL_DECISION.</summary>
    public const string TaskApproved = "Approval.TaskApproved";

    /// <summary>APPROVAL_DECISION.</summary>
    public const string TaskRejected = "Approval.TaskRejected";

    /// <summary>APPROVAL_DECISION.</summary>
    public const string TaskReturned = "Approval.TaskReturned";

    /// <summary>LIFECYCLE_TRANSITION: a task was replaced by one for the escalation role.</summary>
    public const string TaskEscalated = "Approval.TaskEscalated";

    /// <summary>LIFECYCLE_TRANSITION: the run ended; its outcome is published.</summary>
    public const string ApprovalCompleted = "Approval.ApprovalCompleted";

    /// <summary>LIFECYCLE_TRANSITION: the source module applied the outcome.</summary>
    public const string OutcomeDelivered = "Approval.OutcomeDelivered";

    /// <summary>AUTHORIZATION_DENIAL: a decision or escalation by someone without authority over the task at that moment.</summary>
    public const string DecisionRefused = "Approval.DecisionRefused";

    /// <summary>PERMISSION_CHANGE: approval authority was delegated.</summary>
    public const string DelegationCreated = "Approval.DelegationCreated";

    /// <summary>PERMISSION_CHANGE.</summary>
    public const string DelegationRevoked = "Approval.DelegationRevoked";

    /// <summary>PERMISSION_CHANGE: the period ended.</summary>
    public const string DelegationExpired = "Approval.DelegationExpired";
}
