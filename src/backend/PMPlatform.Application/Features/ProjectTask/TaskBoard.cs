using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>A project's tasks and dependencies under the project's task lock, read whole: every task tracked, every dependency.</summary>
internal sealed class TaskBoard(ProjectFacts project, List<ProjectTaskEntity> tasks, List<TaskDependency> dependencies)
{
    public ProjectFacts Project => project;

    public List<ProjectTaskEntity> Tasks => tasks;

    public List<TaskDependency> Dependencies => dependencies;

    public ProjectTaskEntity? Find(Guid taskId) => tasks.Find(t => t.Id == taskId);

    public IEnumerable<ProjectTaskEntity> SubtasksOf(Guid taskId) => tasks.Where(t => t.ParentTaskId == taskId);

    public bool HasLiveSubtasks(Guid taskId) => SubtasksOf(taskId).Any(t => ProjectTaskWorkflow.IsLive(t.Status));

    /// <summary>A live task with no live subtask: one that carries its own percentage and may be a dependency end.</summary>
    public bool IsLiveLeaf(ProjectTaskEntity task) => ProjectTaskWorkflow.IsLive(task.Status) && !HasLiveSubtasks(task.Id);

    public bool IsDependencyEnd(Guid taskId) => dependencies.Exists(d => d.PredecessorTaskId == taskId || d.SuccessorTaskId == taskId);

    public IReadOnlyDictionary<Guid, ProjectTaskEntity> ById() => tasks.ToDictionary(t => t.Id);

    /// <summary>Stamps a changed task's audit columns.</summary>
    public static void Touch(ProjectTaskEntity task, Guid by, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task);
        task.UpdatedAt = now;
        task.UpdatedBy = by;
    }
}
