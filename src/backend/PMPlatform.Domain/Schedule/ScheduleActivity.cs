using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// A node of the work breakdown of the working schedule (TASK-046). A leaf (<see cref="ScheduleActivityKind.Activity"/>)
/// is planned from a requested start and a duration in working days; its planned dates are calculated from them and its
/// dependencies. A node with children is a <see cref="ScheduleActivityKind.Summary"/>, whose dates are rolled up from them.
/// <see cref="PlannedDurationDays"/> is the ADR-009 weight. The forecast is the Current Forecast, kept apart from the
/// planned dates and from every baseline. Delete policy: RETAIN — a mistake is cancelled, never deleted.
/// </summary>
public sealed class ScheduleActivity : AuditedEntity
{
    public Guid ProjectScheduleId { get; set; }

    public Guid? ParentActivityId { get; set; }

    /// <summary>A display code, unique within the schedule; not the activity's identity (the spec's SCH-CC-04).</summary>
    public required string WbsCode { get; set; }

    public required NarrativeText Name { get; set; }

    /// <summary>Derived: a node with children is a summary.</summary>
    public ScheduleActivityKind ActivityKind { get; set; }

    /// <summary>The start the planner asked for; the planned start is the later of it and every dependency constraint.</summary>
    public DateOnly RequestedStartDate { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedFinishDate { get; set; }

    /// <summary>Entered for a leaf; for a summary, the working days its rolled-up dates span.</summary>
    public int PlannedDurationDays { get; set; }

    public DateOnly ForecastStartDate { get; set; }

    public DateOnly ForecastFinishDate { get; set; }

    /// <summary>WF-04's (the spec's DCL-SCH-18); not written by WF-03.</summary>
    public DateOnly? ActualStartDate { get; set; }

    public DateOnly? ActualFinishDate { get; set; }

    public ScheduleActivityStatus Status { get; set; }

    public int SortOrder { get; set; }
}
