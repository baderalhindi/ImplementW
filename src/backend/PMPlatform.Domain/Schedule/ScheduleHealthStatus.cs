using PMPlatform.Domain.Common;

namespace PMPlatform.Domain.Schedule;

/// <summary>
/// The CURRENT/LIVE Schedule Health and project finish variance of a project, one row per project, computed by WF-03
/// against the ACTIVE baseline and rewritten by nothing else; consumers read it and never recompute it. Delete policy: RETAIN.
/// </summary>
public sealed class ScheduleHealthStatus : AuditedEntity
{
    public Guid ProjectId { get; set; }

    /// <summary>The baseline the variance was measured against; null without an ACTIVE baseline.</summary>
    public Guid? ProjectBaselineId { get; set; }

    public ScheduleHealth ScheduleHealth { get; set; }

    /// <summary>Forecast finish − baseline finish, in working days, positive = late; null without a baseline or a forecast.</summary>
    public int? FinishVarianceDays { get; set; }

    public DateTimeOffset ComputedAt { get; set; }

    /// <summary>The WORKFLOW_POLICY version whose thresholds rated it (ERD D-13); null when it is UNKNOWN for want of them.</summary>
    public Guid? HealthRuleConfigurationVersionId { get; set; }
}
