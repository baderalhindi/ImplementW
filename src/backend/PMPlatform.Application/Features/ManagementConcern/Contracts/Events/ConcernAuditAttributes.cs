namespace PMPlatform.Application.Features.ManagementConcern.Contracts.Events;

/// <summary>The attribute names of ManagementConcern's audit events.</summary>
public static class ConcernAuditAttributes
{
    public const string ConcernType = "concern_type";
    public const string Title = "title";
    public const string Description = "description";
    public const string CategoryItemId = "category_item_id";
    public const string PriorityItemId = "priority_item_id";
    public const string TargetResolutionDate = "target_resolution_date";
    public const string OverallImpactLevel = "overall_impact_level";
    public const string SeverityItemId = "severity_item_id";
    public const string SeverityConfigurationVersionId = "severity_configuration_version_id";
    public const string Status = "status";
    public const string RevisionNo = "revision_no";
    public const string AssigneeUserId = "assignee_user_id";
    public const string OriginatingRiskId = "originating_risk_id";
    public const string NextReviewDate = "next_review_date";
    public const string Resolution = "resolution";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string ApprovalDecision = "approval_decision";
    public const string EscalationId = "concern_escalation_id";
    public const string EscalationNo = "escalation_no";
    public const string EscalatedToRoleId = "escalated_to_role_id";
    public const string Reason = "reason";
    public const string Permission = "permission";
}
