namespace PMPlatform.Domain.Schedule;

/// <summary>
/// ERD <c>project_milestone.status</c>. WF-03 plans and cancels a milestone; ACHIEVED is set by WF-03 when WF-05 accepts an
/// achievement of it (ICD-04, TASK-050). ACHIEVED and CANCELLED are final.
/// </summary>
public enum ProjectMilestoneStatus
{
    Planned = 1,
    Achieved = 2,
    Cancelled = 3,
}
