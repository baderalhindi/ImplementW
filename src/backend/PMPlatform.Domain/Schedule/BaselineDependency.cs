using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>The dependency network as its baseline activated, so the baseline is reproducible. Delete policy: APPEND_ONLY.</summary>
public sealed class BaselineDependency : AuditedEntity
{
    public Guid ProjectBaselineId { get; set; }

    public Guid PredecessorActivityId { get; set; }

    public Guid SuccessorActivityId { get; set; }

    public ScheduleDependencyType DependencyType { get; set; }

    public int LagDays { get; set; }
}
