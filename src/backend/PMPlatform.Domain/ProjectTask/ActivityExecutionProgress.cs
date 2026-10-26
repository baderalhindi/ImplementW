using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.ProjectTask;

/// <summary>
/// The actual progress of one schedule activity as WF-04 executes it: the planned-duration-weighted roll-up of the live leaf
/// tasks that execute against it (ADR-009), one row per activity, rewritten by WF-04 on every change that moves it and by
/// nothing else. The fact WF-02 consumes (solution architecture §9, the physical progress pair). Delete policy: RETAIN.
/// </summary>
public sealed class ActivityExecutionProgress : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ScheduleActivityId { get; set; }

    /// <summary>0–100, to four places.</summary>
    public decimal ActualPercentComplete { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
