namespace PMPlatform.Application.Features.ChangeRequest.Contracts.Events;

/// <summary>The attribute names of ChangeRequest's audit events.</summary>
public static class ChangeRequestAuditAttributes
{
    public const string ChangeType = "change_type";
    public const string Title = "title";
    public const string Justification = "justification";
    public const string ScopeImpact = "scope_impact";
    public const string CostImpactSar = "cost_impact_sar";
    public const string ScheduleImpactDays = "schedule_impact_days";
    public const string IsContractualObligation = "is_contractual_obligation";
    public const string RequestedGovernanceProfileItemId = "requested_governance_profile_item_id";
    public const string Status = "status";
    public const string RevisionNo = "revision_no";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string ApprovalDecision = "approval_decision";
    public const string MaterialityEvaluationId = "materiality_evaluation_id";
    public const string MaterialityConfigurationVersionId = "materiality_configuration_version_id";
    public const string ProjectBaselineId = "project_baseline_id";
    public const string FinancialCommitmentId = "financial_commitment_id";
    public const string CumulativeCostImpactSar = "cumulative_cost_impact_sar";
    public const string CumulativeScheduleImpactDays = "cumulative_schedule_impact_days";
    public const string CostBandNo = "cost_band_no";
    public const string ScheduleBandNo = "schedule_band_no";
    public const string ScopeBandNo = "scope_band_no";
    public const string ResultingBandNo = "resulting_band_no";
    public const string ChangeAuthorizationId = "change_authorization_id";
    public const string AuthorizationScope = "authorization_scope";
    public const string TargetModule = "target_module";
    public const string TargetType = "target_type";
    public const string TargetId = "target_id";
    public const string TargetRevisionNo = "target_revision_no";
    public const string AppliedReference = "applied_reference";
    public const string Permission = "permission";
    public const string Reason = "reason";
}
