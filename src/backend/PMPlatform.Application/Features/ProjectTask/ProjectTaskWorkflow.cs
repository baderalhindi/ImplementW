using PMPlatform.Application.Common.Authorization;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// A task's state machine (TASK-048): NOT_STARTED → IN_PROGRESS ⇄ BLOCKED → COMPLETED, a task not yet started may be BLOCKED
/// too, and the two controlled exits: a COMPLETED task is reopened only by the holder of TASK_REOPEN, and a live task is
/// cancelled only by the holder of TASK_MANAGE. There is no BLOCKED → COMPLETED edge: a blocked task is unblocked first.
/// Migration <c>TASK-048_GuardProjectTask</c> refuses every other change of status in the database too.
/// </summary>
internal static class ProjectTaskWorkflow
{
    public static IReadOnlySet<(ProjectTaskStatus From, ProjectTaskStatus To)> Transitions { get; } = new HashSet<(ProjectTaskStatus, ProjectTaskStatus)>
    {
        (ProjectTaskStatus.NotStarted, ProjectTaskStatus.InProgress),   // start
        (ProjectTaskStatus.NotStarted, ProjectTaskStatus.Blocked),      // block before work starts
        (ProjectTaskStatus.InProgress, ProjectTaskStatus.Blocked),      // block
        (ProjectTaskStatus.Blocked, ProjectTaskStatus.NotStarted),      // unblock a task blocked before it started
        (ProjectTaskStatus.Blocked, ProjectTaskStatus.InProgress),      // unblock a started task
        (ProjectTaskStatus.InProgress, ProjectTaskStatus.Completed),    // complete
        (ProjectTaskStatus.Completed, ProjectTaskStatus.InProgress),    // reopen, TASK_REOPEN only
        (ProjectTaskStatus.NotStarted, ProjectTaskStatus.Cancelled),    // cancel, TASK_MANAGE only
        (ProjectTaskStatus.InProgress, ProjectTaskStatus.Cancelled),
        (ProjectTaskStatus.Blocked, ProjectTaskStatus.Cancelled),
    };

    public static bool Allows(ProjectTaskStatus from, ProjectTaskStatus to) => Transitions.Contains((from, to));

    /// <summary>
    /// Where <paramref name="command"/> takes a task in <paramref name="from"/>; null when the state machine has no such edge.
    /// One command per edge family (api-conventions R-4); unblocking returns the task to where it was blocked from, which its
    /// actual start tells.
    /// </summary>
    public static ProjectTaskStatus? TargetOf(TaskCommand command, ProjectTaskStatus from, bool started) => (command, from) switch
    {
        (TaskCommand.Start, ProjectTaskStatus.NotStarted) => ProjectTaskStatus.InProgress,
        (TaskCommand.Block, ProjectTaskStatus.NotStarted or ProjectTaskStatus.InProgress) => ProjectTaskStatus.Blocked,
        (TaskCommand.Unblock, ProjectTaskStatus.Blocked) => started ? ProjectTaskStatus.InProgress : ProjectTaskStatus.NotStarted,
        (TaskCommand.Complete, ProjectTaskStatus.InProgress) => ProjectTaskStatus.Completed,
        (TaskCommand.Reopen, ProjectTaskStatus.Completed) => ProjectTaskStatus.InProgress,
        (TaskCommand.Cancel, ProjectTaskStatus.NotStarted or ProjectTaskStatus.InProgress or ProjectTaskStatus.Blocked) => ProjectTaskStatus.Cancelled,
        _ => null,
    };

    /// <summary>
    /// The permission each command is decided on. Execution is TASK_UPDATE, which reaches the task's owner and the Project
    /// Manager (ADR-009); the controlled exits have their own, so general edit permission never reopens or cancels a task.
    /// </summary>
    public static string PermissionOf(TaskCommand command) => command switch
    {
        TaskCommand.Start or TaskCommand.Block or TaskCommand.Unblock or TaskCommand.Complete => PermissionCatalogue.TaskUpdate,
        TaskCommand.Reopen => PermissionCatalogue.TaskReopen,
        TaskCommand.Cancel => PermissionCatalogue.TaskManage,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown task command."),
    };

    /// <summary>A task still in the plan: every state but CANCELLED.</summary>
    public static bool IsLive(ProjectTaskStatus status) => status != ProjectTaskStatus.Cancelled;
}

/// <summary>The commands that move a task along <see cref="ProjectTaskWorkflow"/>, one per edge family (api-conventions R-4).</summary>
internal enum TaskCommand
{
    Start = 1,
    Block = 2,
    Unblock = 3,
    Complete = 4,
    Reopen = 5,
    Cancel = 6,
}
