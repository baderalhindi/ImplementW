using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// A task or subtask. <see cref="ActualPercentComplete"/> is the figure its owner entered (a leaf's only);
/// <see cref="PercentComplete"/> is the task's progress as ADR-009 rolls it up: a leaf's own, a parent's the planned-duration
/// weighted mean of its live subtasks, 100 once COMPLETED. <see cref="SubtaskCount"/> counts the live subtasks.
/// </summary>
public sealed record ProjectTaskDetail(
    Guid Id,
    Guid ProjectId,
    Guid? ScheduleActivityId,
    Guid? ParentTaskId,
    NarrativeText Title,
    NarrativeText? Description,
    Guid? AssigneeUserId,
    Guid? PriorityItemId,
    ProjectTaskStatus Status,
    DateOnly PlannedStartDate,
    DateOnly PlannedFinishDate,
    int PlannedDurationDays,
    DateOnly? ActualStartDate,
    DateOnly? ActualFinishDate,
    decimal? ActualPercentComplete,
    decimal PercentComplete,
    int SubtaskCount,
    NarrativeText? BlockedReason,
    DateTimeOffset? CompletedAt,
    int ReopenedCount,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);

public sealed record TaskDependencyDetail(
    Guid Id, Guid ProjectId, Guid PredecessorTaskId, Guid SuccessorTaskId, TaskDependencyType DependencyType, DateTimeOffset CreatedAt, Guid CreatedBy);

/// <summary>A schedule activity's actual progress as WF-04 last rolled it up from its tasks (ADR-009): the fact WF-02 consumes.</summary>
public sealed record ActivityExecutionProgressDetail(Guid Id, Guid ProjectId, Guid ScheduleActivityId, decimal ActualPercentComplete, DateTimeOffset ComputedAt);
