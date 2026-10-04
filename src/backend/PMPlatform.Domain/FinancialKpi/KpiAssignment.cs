using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.FinancialKpi;

/// <summary>A catalogue KPI assigned to a project, at most once (TASK-052). Delete policy: RETAIN.</summary>
public sealed class KpiAssignment : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid KpiDefinitionId { get; set; }

    public Guid? OwnerUserId { get; set; }

    /// <summary>A master data item of catalogue MEASUREMENT_FREQUENCY.</summary>
    public Guid MeasurementFrequencyItemId { get; set; }

    public KpiAssignmentStatus Status { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
}
