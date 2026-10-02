namespace PMPlatform.Application.Features.Progress.Contracts.Events;

/// <summary>The attribute names of Progress's audit events.</summary>
public static class ProgressAuditAttributes
{
    public const string Status = "status";
    public const string RevisionNo = "revision_no";
    public const string ReportingCycleId = "reporting_cycle_id";
    public const string PeriodStart = "period_start";
    public const string PeriodEnd = "period_end";
    public const string ActualPercentCalculated = "actual_percent_calculated";
    public const string ActualPercentOverride = "actual_percent_override";
    public const string OverrideReason = "override_reason";
    public const string PlannedPercent = "planned_percent";
    public const string BaselineId = "baseline_id";
    public const string Narrative = "narrative";
    public const string ReturnReason = "return_reason";
    public const string ProjectIntakeId = "project_intake_id";
    public const string SnapshotId = "published_progress_snapshot_id";
    public const string ActualPercent = "actual_percent";
    public const string OverallHealth = "overall_health";
    public const string HealthRuleConfigurationVersionId = "health_rule_configuration_version_id";
    public const string Gate = "gate";
    public const string Reason = "reason";
}
