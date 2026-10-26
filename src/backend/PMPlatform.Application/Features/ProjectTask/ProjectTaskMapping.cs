using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Domain.ProjectTask;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask;

internal static class ProjectTaskMapping
{
    /// <summary>The task with its progress as ADR-009 rolls it up from <paramref name="subtasks"/>, which may hold other tasks' too.</summary>
    public static ProjectTaskDetail ToDetail(ProjectTaskEntity t, IEnumerable<ProjectTaskEntity> subtasks)
    {
        List<ProjectTaskEntity> own = [.. subtasks.Where(s => s.ParentTaskId == t.Id)];
        return new ProjectTaskDetail(
            t.Id, t.ProjectId, t.ScheduleActivityId, t.ParentTaskId, t.Title, t.Description, t.AssigneeUserId, t.PriorityItemId, t.Status, t.PlannedStartDate,
            t.PlannedFinishDate, t.PlannedDurationDays, t.ActualStartDate, t.ActualFinishDate, t.ActualPercentComplete, TaskProgressRollup.PercentOf(t, own),
            own.Count(s => ProjectTaskWorkflow.IsLive(s.Status)), t.BlockedReason, t.CompletedAt, t.ReopenedCount, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy);
    }

    public static TaskDependencyDetail ToDetail(TaskDependency d, Guid projectId) =>
        new(d.Id, projectId, d.PredecessorTaskId, d.SuccessorTaskId, d.DependencyType, d.CreatedAt, d.CreatedBy);

    public static ActivityExecutionProgressDetail ToDetail(ActivityExecutionProgress p) =>
        new(p.Id, p.ProjectId, p.ScheduleActivityId, p.ActualPercentComplete, p.ComputedAt);
}
