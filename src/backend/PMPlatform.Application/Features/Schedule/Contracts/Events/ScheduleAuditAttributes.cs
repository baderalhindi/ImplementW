namespace PMPlatform.Application.Features.Schedule.Contracts.Events;

/// <summary>The attribute names of Schedule's audit events.</summary>
public static class ScheduleAuditAttributes
{
    public const string ProjectScheduleId = "project_schedule_id";
    public const string ParentActivityId = "parent_activity_id";
    public const string WbsCode = "wbs_code";
    public const string Name = "name";
    public const string Status = "status";
    public const string RequestedStartDate = "requested_start_date";
    public const string PlannedDurationDays = "planned_duration_days";
    public const string SortOrder = "sort_order";
    public const string ForecastStartDate = "forecast_start_date";
    public const string ForecastFinishDate = "forecast_finish_date";
    public const string RecalculatedActivityCount = "recalculated_activity_count";
    public const string PredecessorActivityId = "predecessor_activity_id";
    public const string SuccessorActivityId = "successor_activity_id";
    public const string DependencyType = "dependency_type";
    public const string LagDays = "lag_days";
    public const string BaselineType = "baseline_type";
    public const string VersionNo = "version_no";
    public const string RevisionNo = "revision_no";
    public const string BaselineFinishDate = "baseline_finish_date";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string ApprovalRequired = "approval_required";
    public const string ChangeAuthorizationId = "change_authorization_id";
    public const string SupersededBaselineId = "superseded_baseline_id";
    public const string SupersededByBaselineId = "superseded_by_baseline_id";
    public const string Reason = "reason";
    public const string SubjectRevisionNo = "subject_revision_no";
    public const string Decision = "decision";
    public const string ProjectIntakeId = "project_intake_id";
    public const string DeclaredEndDate = "declared_end_date";
    public const string ScheduleHealth = "schedule_health";
    public const string FinishVarianceDays = "finish_variance_days";
    public const string ProjectBaselineId = "project_baseline_id";
    public const string HealthRuleConfigurationVersionId = "health_rule_configuration_version_id";
}
