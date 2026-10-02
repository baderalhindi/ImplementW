using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Progress;

/// <summary>
/// The CURRENT/LIVE Overall Project Health of a project, one row per project, rewritten by WF-02 on each recompute and
/// by nothing else (ICD-03, M-12). It is computed from the derived figures, never from an unpublished override, and is
/// distinct from the PUBLISHED/OFFICIAL value in <see cref="PublishedProgressSnapshot"/>. Delete policy: RETAIN.
/// </summary>
public sealed class ProjectHealthStatus : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public HealthStatus OverallHealth { get; set; }

    public decimal? ActualPercent { get; set; }

    public decimal? PlannedPercent { get; set; }

    /// <summary>Freshness metadata for FG-01.</summary>
    public DateTimeOffset ComputedAt { get; set; }

    public Guid HealthRuleConfigurationVersionId { get; set; }
}
