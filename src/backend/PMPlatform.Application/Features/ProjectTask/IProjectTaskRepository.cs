using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// The <c>project_task</c> schema (TASK-048). Finds that return rows to change track them; the others do not. Every write runs
/// inside <see cref="BeginAsync"/>'s unit of work after <see cref="LockProjectAsync"/>, so the writes to one project's tasks,
/// dependencies and Activity Execution Progress happen one at a time.
/// </summary>
public interface IProjectTaskRepository
{
    public Task<IProjectTaskWork> BeginAsync(CancellationToken cancellationToken);

    /// <summary>Takes the project's task lock until the unit of work ends; the database guards take the same lock.</summary>
    public Task LockProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Every task of the project, cancelled ones included; tracked when <paramref name="track"/>.</summary>
    public Task<IReadOnlyList<ProjectTaskEntity>> ListTasksAsync(Guid projectId, bool track, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the project's tasks — of those assigned to <paramref name="assigneeUserId"/> when one is given — each parent
    /// followed by its subtasks, in creation order, with the total. Not tracked.
    /// </summary>
    public Task<(IReadOnlyList<ProjectTaskEntity> Items, int TotalCount)> PageTasksAsync(
        Guid projectId, Guid? assigneeUserId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The subtasks of the given tasks, cancelled ones included. Not tracked.</summary>
    public Task<IReadOnlyList<ProjectTaskEntity>> ListSubtasksAsync(IReadOnlyCollection<Guid> parentTaskIds, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<ProjectTaskEntity?> FindTaskAsync(Guid taskId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The task as stored now, with its row version, read past anything this unit of work tracks. Not tracked.</summary>
    public Task<(ProjectTaskEntity Task, uint Version)?> ReadTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Every dependency between the project's tasks. Not tracked.</summary>
    public Task<IReadOnlyList<TaskDependency>> ListDependenciesAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>One page of them, oldest first, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<TaskDependency> Items, int TotalCount)> PageDependenciesAsync(Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<TaskDependency?> FindDependencyAsync(Guid dependencyId, CancellationToken cancellationToken);

    /// <summary>The project's Activity Execution Progress rows. Tracked.</summary>
    public Task<IReadOnlyList<ActivityExecutionProgress>> ListActivityProgressAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>One page of them, by schedule activity, with the total. Not tracked.</summary>
    public Task<(IReadOnlyList<ActivityExecutionProgress> Items, int TotalCount)> PageActivityProgressAsync(
        Guid projectId, PageRequest page, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked row, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(ProjectTaskEntity task);

    public uint RowVersionOf(TaskDependency dependency);

    public void Add(ProjectTaskEntity task);

    public void Add(TaskDependency dependency);

    public void Add(ActivityExecutionProgress progress);

    /// <summary>HARD_WORKING.</summary>
    public void Remove(TaskDependency dependency);

    /// <summary>
    /// Saves the tracked changes and the audit events this unit of work staged. A row changed since it was read, and a unique
    /// key another request took first, are answers, not faults; after either nothing stays tracked and the unit of work is lost.
    /// </summary>
    public Task<ProjectTaskSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A unit of work over a project's tasks; disposing it without <see cref="CommitAsync"/> rolls it back.</summary>
public interface IProjectTaskWork : IAsyncDisposable
{
    public Task CommitAsync(CancellationToken cancellationToken);
}

public enum ProjectTaskSaveOutcome
{
    Saved = 1,

    /// <summary>The row changed since it was read (R-21).</summary>
    ConcurrencyConflict = 2,

    /// <summary>A unique key another request took first: a dependency, or an activity's progress row.</summary>
    Duplicate = 3,
}
