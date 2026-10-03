using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// A project's working schedule: the container of its activities and their Current Forecast (TASK-046), one per project.
/// Every write to the schedule or to the project's baselines locks this row first, so they happen one at a time per
/// project. Delete policy: RETAIN.
/// </summary>
public sealed class ProjectSchedule : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The working calendar of duration arithmetic. No calendar is configured yet (schedule-baseline.md F-3), so it is never set.</summary>
    public Guid? CalendarItemId { get; set; }
}
