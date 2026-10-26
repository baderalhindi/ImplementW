using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// An activity's planned dates and duration as its baseline activated: the reference of variance and of planned
/// progress (ADR-009), never changed. Delete policy: APPEND_ONLY.
/// </summary>
public sealed class BaselineActivity : AuditedEntity
{
    public Guid ProjectBaselineId { get; set; }

    public Guid ScheduleActivityId { get; set; }

    /// <summary>The parent as the baseline activated, null at the top, so the baseline's hierarchy is reproducible.</summary>
    public Guid? ParentActivityId { get; set; }

    /// <summary>As the baseline activated: planned progress weights its leaves only (ADR-009).</summary>
    public ScheduleActivityKind ActivityKind { get; set; }

    public DateOnly PlannedStartDate { get; set; }

    public DateOnly PlannedFinishDate { get; set; }

    public int PlannedDurationDays { get; set; }
}
