namespace PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;

/// <summary>The attribute names of ExternalParticipation's audit events.</summary>
public static class ExternalParticipationAuditAttributes
{
    public const string ExternalEntityId = "external_entity_id";
    public const string ContributionTypeItemId = "contribution_type_item_id";
    public const string TargetModule = "target_module";
    public const string TargetType = "target_type";
    public const string TargetId = "target_id";
    public const string Instructions = "instructions";
    public const string ResponsibleUserId = "responsible_user_id";
    public const string ReviewerUserId = "reviewer_user_id";
    public const string DueDate = "due_date";
    public const string Status = "status";
    public const string Reason = "reason";
    public const string ExternalContributionId = "external_contribution_id";
    public const string RevisionNo = "revision_no";
    public const string NextContributionId = "next_external_contribution_id";
    public const string FieldCodes = "field_codes";
    public const string TargetVersion = "target_version";
    public const string TargetState = "target_state";
    public const string InternalNote = "internal_note";
    public const string SourceApplicationId = "source_application_id";
    public const string AttemptNo = "attempt_no";
    public const string ExpectedTargetVersion = "expected_target_version";
    public const string ActualTargetVersion = "actual_target_version";
    public const string FailureCode = "failure_code";
    public const string Permission = "permission";
}
