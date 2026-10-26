namespace PMPlatform.Application.Features.ProjectTask.Contracts.Events;

/// <summary>
/// The audit event types ProjectTask produces (TASK-048; event-conventions EV-1), recorded through <c>IAuditTrail</c> in the
/// producer's unit of work. The list is appended to event-conventions.md §4 with this task.
/// </summary>
public static class ProjectTaskAuditEvents
{
    /// <summary>DATA_CHANGE: a task or subtask was added.</summary>
    public const string TaskCreated = "ProjectTask.TaskCreated";

    /// <summary>DATA_CHANGE: a task's plan or assignment changed (title and description withheld).</summary>
    public const string TaskChanged = "ProjectTask.TaskChanged";

    /// <summary>DATA_CHANGE: a leaf's actual percentage was entered, by its owner or the Project Manager (ADR-009).</summary>
    public const string TaskProgressReported = "ProjectTask.TaskProgressReported";

    /// <summary>LIFECYCLE_TRANSITION: NOT_STARTED → IN_PROGRESS.</summary>
    public const string TaskStarted = "ProjectTask.TaskStarted";

    /// <summary>LIFECYCLE_TRANSITION: → BLOCKED (the reason withheld).</summary>
    public const string TaskBlocked = "ProjectTask.TaskBlocked";

    /// <summary>LIFECYCLE_TRANSITION: BLOCKED → IN_PROGRESS or NOT_STARTED.</summary>
    public const string TaskUnblocked = "ProjectTask.TaskUnblocked";

    /// <summary>LIFECYCLE_TRANSITION: IN_PROGRESS → COMPLETED.</summary>
    public const string TaskCompleted = "ProjectTask.TaskCompleted";

    /// <summary>LIFECYCLE_TRANSITION: COMPLETED → IN_PROGRESS, with the task's reopen count.</summary>
    public const string TaskReopened = "ProjectTask.TaskReopened";

    /// <summary>LIFECYCLE_TRANSITION: → CANCELLED.</summary>
    public const string TaskCancelled = "ProjectTask.TaskCancelled";

    /// <summary>DATA_CHANGE.</summary>
    public const string DependencyCreated = "ProjectTask.DependencyCreated";

    /// <summary>DATA_CHANGE.</summary>
    public const string DependencyDeleted = "ProjectTask.DependencyDeleted";

    /// <summary>DATA_CHANGE: an activity's Activity Execution Progress changed.</summary>
    public const string ActivityProgressRecomputed = "ProjectTask.ActivityProgressRecomputed";
}
