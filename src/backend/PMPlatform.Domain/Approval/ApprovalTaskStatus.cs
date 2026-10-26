namespace PMPlatform.Domain.Approval;

/// <summary>ERD <c>approval_task.status</c>. Every state but PENDING is terminal.</summary>
public enum ApprovalTaskStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Returned = 4,

    /// <summary>In the ERD's value set; no operation produces it (a delegate acts on the task itself). Record F-5.</summary>
    Delegated = 5,

    /// <summary>Replaced by the task in <see cref="ApprovalTask.EscalatedToTaskId"/>.</summary>
    Escalated = 6,

    /// <summary>Closed without a decision: the run ended, or its stage completed without this optional task.</summary>
    Cancelled = 7,

    /// <summary>In the ERD's value set; no operation produces it (an overdue task is escalated). Record F-5.</summary>
    Expired = 8,
}
