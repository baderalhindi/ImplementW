namespace PMPlatform.Domain.Closure;

/// <summary>ERD <c>post_project_obligation.status</c> (TASK-063): OPEN → IN_PROGRESS, and either to SATISFIED, WAIVED or CANCELLED, which are final.</summary>
public enum PostProjectObligationStatus
{
    Open = 1,
    InProgress = 2,
    Satisfied = 3,
    Waived = 4,
    Cancelled = 5,
}
