namespace PMPlatform.Domain.ManagementConcern;

/// <summary>
/// ERD <c>management_concern.status</c> (TASK-057): Open → Assigned → In Progress → Pending Validation → Resolved → Closed.
/// Validation is decided through WF-11; a return sends the concern back to IN_PROGRESS. CLOSED is final.
/// </summary>
public enum ConcernStatus
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    PendingValidation = 4,
    Resolved = 5,
    Closed = 6,
}
