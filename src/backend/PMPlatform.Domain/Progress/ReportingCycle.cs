using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Progress;

/// <summary>
/// A reporting period of a project, generated from its governance profile's update cadence (TASK-044, ADR-015). The
/// ADR-014 opening position has a one-day period of its own, the intake date. Delete policy: RETAIN.
/// </summary>
public sealed class ReportingCycle : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public DateOnly DueDate { get; set; }

    public ReportingCycleStatus Status { get; set; }
}
