using PMPlatform.Domain.Common;
using PMPlatform.Domain.ProjectTask;

namespace PMPlatform.Application.Features.ProjectTask.Contracts;

/// <summary>
/// A new task, or a subtask when <see cref="ParentTaskId"/> is set. A subtask executes against its parent's schedule activity,
/// so it names none or the same one. The planned duration is derived from the dates, never sent.
/// </summary>
public sealed record ProjectTaskDraft(
    Guid ProjectId,
    Guid? ParentTaskId,
    Guid? ScheduleActivityId,
    NarrativeText Title,
    NarrativeText? Description,
    Guid? AssigneeUserId,
    Guid? PriorityItemId,
    DateOnly PlannedStartDate,
    DateOnly PlannedFinishDate);

/// <summary>A task's plan and assignment, as a whole (R-5). Its project and parent never change.</summary>
public sealed record ProjectTaskChanges(
    Guid? ScheduleActivityId,
    NarrativeText Title,
    NarrativeText? Description,
    Guid? AssigneeUserId,
    Guid? PriorityItemId,
    DateOnly PlannedStartDate,
    DateOnly PlannedFinishDate);

public sealed record TaskDependencyDraft(Guid PredecessorTaskId, Guid SuccessorTaskId, TaskDependencyType DependencyType);
