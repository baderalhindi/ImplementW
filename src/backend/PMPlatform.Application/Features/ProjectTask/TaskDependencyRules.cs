using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// The blocking rules of task dependencies (TASK-048). FS and SS gate the successor's start, FF and SF its completion; the
/// predecessor meets an F by being COMPLETED and an S by having started. The network is acyclic (<c>DependencyGraph</c>).
/// Migration <c>TASK-048_GuardProjectTask</c> holds the same rules in the database.
/// </summary>
internal static class TaskDependencyRules
{
    /// <summary>Whether a task has started: it has an actual start, which unblocking a never-started task does not give it.</summary>
    public static bool HasStarted(ProjectTaskEntity task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.ActualStartDate is not null;
    }

    /// <summary>Whether a dependency of <paramref name="type"/> gates the successor's start (else its completion).</summary>
    public static bool GatesStart(TaskDependencyType type) => type is TaskDependencyType.Fs or TaskDependencyType.Ss;

    /// <summary>Whether <paramref name="predecessor"/> has reached the point a dependency of <paramref name="type"/> waits for.</summary>
    public static bool IsMet(TaskDependencyType type, ProjectTaskEntity predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        return type is TaskDependencyType.Fs or TaskDependencyType.Ff ? predecessor.Status == ProjectTaskStatus.Completed : HasStarted(predecessor);
    }

    /// <summary>The predecessors still keeping <paramref name="successorId"/> from its start (or, if not <paramref name="start"/>, its completion).</summary>
    public static IReadOnlyList<Guid> Unmet(
        Guid successorId, bool start, IEnumerable<TaskDependency> dependencies, IReadOnlyDictionary<Guid, ProjectTaskEntity> tasks)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(tasks);
        return
        [
            .. dependencies
                .Where(d => d.SuccessorTaskId == successorId && GatesStart(d.DependencyType) == start && !IsMet(d.DependencyType, tasks[d.PredecessorTaskId]))
                .Select(d => d.PredecessorTaskId),
        ];
    }

    /// <summary>Whether a new dependency would already be broken: the successor took the step it gates before the predecessor got there.</summary>
    public static bool IsBrokenOnArrival(TaskDependencyType type, ProjectTaskEntity predecessor, ProjectTaskEntity successor)
    {
        ArgumentNullException.ThrowIfNull(successor);
        bool stepTaken = GatesStart(type) ? HasStarted(successor) : successor.Status == ProjectTaskStatus.Completed;
        return stepTaken && !IsMet(type, predecessor);
    }
}
