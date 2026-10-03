namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>The ProjectTask module's error codes (api-conventions R-27). A code, once shipped, keeps its meaning.</summary>
public static class ProjectTaskErrorCodes
{
    /// <summary>422: tasks are planned while the project is APPROVED_PLANNED or ACTIVE, and executed while it is ACTIVE.</summary>
    public const string ProjectNotEligible = "TASK_PROJECT_NOT_ELIGIBLE";

    /// <summary>409: the task is CANCELLED and changes no more.</summary>
    public const string NotEditable = "TASK_NOT_EDITABLE";

    /// <summary>
    /// 422: the parent is not a live, not completed top-level task of the project (one level of subtasks), or it is a dependency
    /// end and so cannot take subtasks.
    /// </summary>
    public const string HierarchyInvalid = "TASK_HIERARCHY_INVALID";

    /// <summary>422: the schedule activity is not a live leaf activity of the project, or a subtask names another than its parent's.</summary>
    public const string ScheduleActivityInvalid = "TASK_SCHEDULE_ACTIVITY_INVALID";

    /// <summary>422: the assignee holds no role over the project now.</summary>
    public const string AssigneeNotEligible = "TASK_ASSIGNEE_NOT_ELIGIBLE";

    /// <summary>422: the priority is not a PUBLISHED item of the PRIORITY catalogue.</summary>
    public const string PriorityInvalid = "TASK_PRIORITY_INVALID";

    /// <summary>
    /// 422: a predecessor has not reached the point a dependency waits for — completed for FS and FF, started for SS and SF — so the
    /// task cannot start or complete yet; or, for a new dependency, the successor has already taken the step it would gate.
    /// </summary>
    public const string DependencyUnmet = "TASK_DEPENDENCY_UNMET";

    /// <summary>409: a parent completes only when every live subtask is COMPLETED, and a subtask reopens only under a parent that is not.</summary>
    public const string SubtasksOpen = "TASK_SUBTASKS_OPEN";

    /// <summary>409: a task is cancelled only when it has no live subtask and no dependency.</summary>
    public const string TaskInUse = "TASK_IN_USE";

    /// <summary>422: an actual percentage is entered on a leaf that is IN_PROGRESS or BLOCKED only; a parent's is rolled up.</summary>
    public const string ProgressNotEnterable = "TASK_PROGRESS_NOT_ENTERABLE";

    /// <summary>422: the dependency's ends are not two distinct live leaf tasks of one project: a parent is no dependency end.</summary>
    public const string DependencyInvalid = "TASK_DEPENDENCY_INVALID";

    /// <summary>422: the dependency would close a cycle in the project's task network.</summary>
    public const string DependencyCircular = "TASK_DEPENDENCY_CIRCULAR";

    /// <summary>409: the two tasks are already linked in that direction.</summary>
    public const string DependencyExists = "TASK_DEPENDENCY_EXISTS";
}
