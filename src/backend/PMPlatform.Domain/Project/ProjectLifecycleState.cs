namespace PMPlatform.Domain.Project;

/// <summary>
/// TASK-041 (Draft → Submitted → Under Review → Returned / Approved-Planned; Planned → Active), TASK-062
/// (Active ↔ Suspended), TASK-063 (Completed → Closed; Closed is terminal).
/// </summary>
public enum ProjectLifecycleState
{
    Draft = 1,
    Submitted = 2,
    UnderReview = 3,
    Returned = 4,
    ApprovedPlanned = 5,
    Active = 6,
    Suspended = 7,
    Completed = 8,
    Closed = 9,
}
