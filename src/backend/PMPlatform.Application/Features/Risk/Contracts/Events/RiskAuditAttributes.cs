namespace PMPlatform.Application.Features.Risk.Contracts.Events;

/// <summary>The attribute names of Risk's audit events.</summary>
public static class RiskAuditAttributes
{
    public const string Title = "title";
    public const string Description = "description";
    public const string RiskCategoryItemId = "risk_category_item_id";
    public const string OwnerUserId = "owner_user_id";
    public const string IdentifiedDate = "identified_date";
    public const string NextReviewDate = "next_review_date";
    public const string Status = "status";
    public const string ClosureRationale = "closure_rationale";
    public const string ReopenedCount = "reopened_count";
    public const string AssessmentVersionNo = "assessment_version_no";
    public const string MatrixConfigurationVersionId = "matrix_configuration_version_id";
    public const string ProbabilityLevel = "probability_level";
    public const string OverallImpactLevel = "overall_impact_level";
    public const string RiskRatingDefinitionId = "risk_rating_definition_id";
    public const string RatingCode = "rating_code";
    public const string AcceptanceId = "risk_acceptance_id";
    public const string ExpiresOn = "expires_on";
    public const string ManagementConcernId = "management_concern_id";
    public const string Permission = "permission";
    public const string Reason = "reason";
    public const string ActionType = "action_type";
    public const string DueDate = "due_date";
}
