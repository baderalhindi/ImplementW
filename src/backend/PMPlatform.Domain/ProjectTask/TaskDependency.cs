using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ProjectTask;

/// <summary>
/// A blocking dependency between two tasks of one project (TASK-048): the successor cannot start, or cannot complete, until the
/// predecessor has reached the point its <see cref="DependencyType"/> names. The graph is acyclic. A dependency is never
/// changed, only removed and added again. Delete policy: HARD_WORKING.
/// </summary>
public sealed class TaskDependency : AuditedEntity
{
    public Guid PredecessorTaskId { get; set; }

    public Guid SuccessorTaskId { get; set; }

    public TaskDependencyType DependencyType { get; set; }
}
