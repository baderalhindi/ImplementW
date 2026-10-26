using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// WF-04's tasks and subtasks (TASK-048): their plan, and the Activity Execution Progress rolled up from them. Each operation is
/// decided by the authorization engine on the project's anchors, with the task's owner as its assignee. A collection is of one
/// project and is empty for a project the caller may not see; a task the caller may not see is 404 (R-47).
/// </summary>
public interface IProjectTaskService
{
    /// <summary>The project's tasks the caller may see, top-level tasks first, each followed by its subtasks.</summary>
    public Task<ProjectTaskPage> ListTasksAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> GetTaskAsync(Guid callerId, Guid taskId, CancellationToken cancellationToken);

    /// <summary>Adds a task, or a subtask of a live top-level task, NOT_STARTED, while the project is APPROVED_PLANNED or ACTIVE.</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> CreateTaskAsync(Guid callerId, ProjectTaskDraft draft, CancellationToken cancellationToken);

    /// <summary>Replaces a live task's plan and assignment. Requires the caller's version (R-21).</summary>
    public Task<AdministrationResult<Versioned<ProjectTaskDetail>>> UpdateTaskAsync(
        Guid callerId, Guid taskId, ProjectTaskChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>The project's Activity Execution Progress, one row per schedule activity its tasks execute against.</summary>
    public Task<ActivityExecutionProgressPage> ListActivityProgressAsync(Guid callerId, Guid projectId, PageRequest page, CancellationToken cancellationToken);
}
