namespace PMPlatform.Domain.ProjectTask;

/// <summary>ERD <c>project_task.status</c> (TASK-048). CANCELLED is final; COMPLETED is left only by a reopen.</summary>
public enum ProjectTaskStatus
{
    NotStarted = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5,
}
