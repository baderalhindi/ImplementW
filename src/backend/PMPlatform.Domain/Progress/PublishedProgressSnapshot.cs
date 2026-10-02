using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Progress;

/// <summary>
/// The PUBLISHED/OFFICIAL record of a reporting period, including the Overall Project Health computed by WF-02 and
/// nowhere else (ICD-03). Self-contained: every figure is a copy taken at publication, so a later correction anywhere
/// changes nothing here. Delete policy: APPEND_ONLY — never updated, never deleted.
/// </summary>
public sealed class PublishedProgressSnapshot : AuditedEntity
{
    public Guid ProjectId { get; set; }

    public Guid ReportingCycleId { get; set; }

    public Guid ProgressSubmissionId { get; set; }

    public DateTimeOffset PublishedAt { get; set; }

    public Guid PublishedByUserId { get; set; }

    /// <summary>
    /// The effective actual: the override when there was one, else the calculated value. The calculated value stays on the
    /// published submission, which never changes again (ADR-009).
    /// </summary>
    public decimal ActualPercent { get; set; }

    public bool IsOverridden { get; set; }

    public decimal? PlannedPercent { get; set; }

    public HealthStatus OverallHealth { get; set; }

    /// <summary>WF-03's Schedule Health at publication; null when there was none to copy.</summary>
    public HealthStatus? ScheduleHealth { get; set; }

    /// <summary>WF-14's financial status at publication; null when there was none to copy.</summary>
    public HealthStatus? FinancialStatus { get; set; }

    /// <summary>The WORKFLOW_POLICY version whose health rule computed <see cref="OverallHealth"/> (ERD D-13).</summary>
    public Guid HealthRuleConfigurationVersionId { get; set; }
}
