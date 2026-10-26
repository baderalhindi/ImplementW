using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ProjectTask;

/// <summary>
/// A task of a project's execution (WF-04, TASK-048), or a subtask when <see cref="ParentTaskId"/> is set: one level of nesting,
/// and a subtask executes against its parent's schedule activity. The owner (<see cref="AssigneeUserId"/>) maintains the actual
/// percentage of a leaf, which the Project Manager may also edit (ADR-009); a parent's percentage is rolled up from its subtasks
/// on read and never stored. <see cref="PlannedDurationDays"/> is the roll-up weight. Named ProjectTask, not Task, so it does
/// not shadow <c>System.Threading.Tasks.Task</c> (solution architecture §4.4b). Delete policy: RETAIN — a mistake is cancelled.
/// </summary>
public sealed class ProjectTask : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The WF-03 activity the task executes against, held as an identifier (ADR-003 §8.2 edge 9); null for one that does not roll up.</summary>
    public Guid? ScheduleActivityId { get; set; }

    public Guid? ParentTaskId { get; set; }

    public required NarrativeText Title { get; set; }

    public NarrativeText? Description { get; set; }

    /// <summary>The task owner.</summary>
    public Guid? AssigneeUserId { get; set; }

    /// <summary>A PRIORITY master data item.</summary>
    public Guid? PriorityItemId { get; set; }

    public ProjectTaskStatus Status { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedFinishDate { get; set; }

    /// <summary>Derived from the planned dates and stored (ERD §7 row 2): the ADR-009 weight.</summary>
    public int PlannedDurationDays { get; set; }

    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualFinishDate { get; set; }

    /// <summary>0–100, entered for a leaf only; 100 on completion.</summary>
    public decimal? ActualPercentComplete { get; set; }

    /// <summary>Why the task is BLOCKED; set exactly while it is.</summary>
    public NarrativeText? BlockedReason { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>How many times the task was reopened after completion, each by the holder of the reopen permission.</summary>
    public int ReopenedCount { get; set; }
}
