using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

/// <summary>
/// ADR-009 at task level: a leaf's actual percentage is the one its owner entered (100 once COMPLETED, 0 until entered); a
/// parent's is the planned-duration-weighted mean of its live subtasks, computed on read and never stored; an activity's
/// Activity Execution Progress is the same mean over the live leaf tasks that execute against it. A CANCELLED task is out of
/// every roll-up. Figures are percentages 0–100, rounded to the four places the columns hold, as WF-02 rounds them.
/// </summary>
internal static class TaskProgressRollup
{
    private const int Places = 4;

    /// <summary>The task's percentage: its own as a leaf, or its live subtasks' weighted mean. A COMPLETED task is 100.</summary>
    public static decimal PercentOf(ProjectTaskEntity task, IEnumerable<ProjectTaskEntity> subtasks)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(subtasks);
        if (task.Status == ProjectTaskStatus.Completed)
        {
            return 100m;
        }

        List<ProjectTaskEntity> live = [.. subtasks.Where(s => s.ParentTaskId == task.Id && ProjectTaskWorkflow.IsLive(s.Status))];
        return live.Count == 0 ? LeafPercent(task) : Weighted(live)!.Value;
    }

    /// <summary>
    /// The activity's actual percentage from <paramref name="tasks"/>, the project's tasks: the weighted mean of the live leaves
    /// executing against it. Null when none does.
    /// </summary>
    public static decimal? ActivityPercent(Guid scheduleActivityId, IReadOnlyCollection<ProjectTaskEntity> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        HashSet<Guid> parents = [.. tasks.Where(t => t.ParentTaskId is not null && ProjectTaskWorkflow.IsLive(t.Status)).Select(t => t.ParentTaskId!.Value)];
        return Weighted(tasks.Where(t => t.ScheduleActivityId == scheduleActivityId && ProjectTaskWorkflow.IsLive(t.Status) && !parents.Contains(t.Id)));
    }

    /// <summary>A leaf's percentage as entered: 100 once COMPLETED, 0 until its owner enters one.</summary>
    public static decimal LeafPercent(ProjectTaskEntity task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return task.Status == ProjectTaskStatus.Completed ? 100m : task.ActualPercentComplete ?? 0m;
    }

    private static decimal? Weighted(IEnumerable<ProjectTaskEntity> leaves)
    {
        decimal weighted = 0;
        decimal duration = 0;
        foreach (ProjectTaskEntity leaf in leaves)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(leaf.PlannedDurationDays, 1, nameof(leaves));
            weighted += LeafPercent(leaf) * leaf.PlannedDurationDays;
            duration += leaf.PlannedDurationDays;
        }

        return duration == 0 ? null : Math.Round(weighted / duration, Places, MidpointRounding.AwayFromZero);
    }
}
