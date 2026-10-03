namespace PMPlatform.Application.Features.Milestone.Contracts.Events;

/// <summary>The attribute names of Milestone's audit events.</summary>
public static class MilestoneAuditAttributes
{
    public const string ProjectMilestoneId = "project_milestone_id";
    public const string RevisionNo = "revision_no";
    public const string Status = "status";
    public const string ClaimedAchievementDate = "claimed_achievement_date";
    public const string AcceptedActualAchievementDate = "accepted_actual_achievement_date";
    public const string Narrative = "narrative";
    public const string CorrectsAchievementId = "corrects_achievement_id";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string Decision = "decision";
    public const string Reason = "reason";
    public const string SubjectRevisionNo = "subject_revision_no";
    public const string EvidencePolicyVersionId = "evidence_policy_version_id";
    public const string SupersededAchievementId = "superseded_achievement_id";
    public const string SupersededByAchievementId = "superseded_by_achievement_id";
    public const string DocumentId = "document_id";
    public const string DocumentVersionId = "document_version_id";
    public const string EvidenceReferenceId = "evidence_reference_id";
    public const string EvidenceTypeItemId = "evidence_type_item_id";
}
