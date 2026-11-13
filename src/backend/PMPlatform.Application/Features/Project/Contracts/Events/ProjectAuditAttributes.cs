namespace PMPlatform.Application.Features.Project.Contracts.Events;

/// <summary>The attribute names of Project's audit events.</summary>
public static class ProjectAuditAttributes
{
    public const string Status = "status";
    public const string RevisionNo = "revision_no";
    public const string SubjectRevisionNo = "subject_revision_no";
    public const string FormalProjectId = "formal_project_id";
    public const string TitleText = "title";
    public const string DescriptionText = "description";
    public const string ClassificationItemId = "classification_item_id";
    public const string DepartmentId = "department_id";
    public const string ExternalEntityId = "external_entity_id";
    public const string ParticipationMode = "participation_mode";
    public const string GovernanceProfileItemId = "governance_profile_item_id";
    public const string RegistrationBudgetSar = "registration_budget_sar";
    public const string PlannedStartDate = "planned_start_date";
    public const string PlannedEndDate = "planned_end_date";
    public const string RegionItemId = "region_item_id";
    public const string CityItemId = "city_item_id";
    public const string Latitude = "latitude";
    public const string Longitude = "longitude";
    public const string ProjectManagerUserId = "project_manager_user_id";
    public const string ApprovalInstanceId = "approval_instance_id";
    public const string Decision = "decision";
    public const string ActivatedAt = "activated_at";
    public const string Gate = "gate";
    public const string Reason = "reason";
    public const string SuspensionRequestId = "suspension_request_id";
    public const string CompletionCaseId = "completion_case_id";
    public const string ClosureCaseId = "closure_case_id";
    public const string ClosedAt = "closed_at";
    public const string LegacyIntakeDate = "legacy_intake_date";
    public const string ProjectIntakeId = "project_intake_id";
    public const string DeclaredBudgetSar = "declared_budget_sar";
    public const string DeclaredEndDate = "declared_end_date";
    public const string OpeningPercentComplete = "opening_percent_complete";
    public const string OpeningSpendToDateSar = "opening_spend_to_date_sar";
    public const string StrategicPortfolioItemId = "strategic_portfolio_item_id";
    public const string StrategicInitiativeItemId = "strategic_initiative_item_id";
    public const string PriorityItemId = "priority_item_id";
    public const string FundingSource = "funding_source";
    public const string LegacyReference = "legacy_reference";
    public const string FollowUpOwnerUserId = "follow_up_owner_user_id";
    public const string ProposedProjectManagerUserId = "proposed_project_manager_user_id";
    public const string CurrentObstacleText = "current_obstacle_text";
    public const string NextActionText = "next_action_text";
    public const string NextActionOwner = "next_action_owner";
    public const string NextActionDueDate = "next_action_due_date";
    public const string DeclaredPlannedPercent = "declared_planned_percent";
    public const string DeclaredActualPercent = "declared_actual_percent";
    public const string DeclaredPlannedSpendPercent = "declared_planned_spend_percent";
    public const string DeclaredActualSpendPercent = "declared_actual_spend_percent";
    public const string DeclaredAsOf = "declared_as_of";
    public const string DeclaredSource = "declared_source";
    public const string EntityUpdateCompliant = "entity_update_compliant";
    public const string FollowUpNoteId = "follow_up_note_id";
    public const string StagePlanRevisionNo = "stage_plan_revision_no";
    public const string ChangeAuthorizationId = "change_authorization_id";
    public const string ReasonText = "reason_text";

    /// <summary>One stage's plan, <c>stage_plan_{code}</c> in lower case: whether it applies, its planned start and end, and its starting %.</summary>
    public static string StagePlanOf(string stageCode) => $"stage_plan_{stageCode.ToLowerInvariant()}";
    public const string ProjectImportBatchId = "project_import_batch_id";
    public const string BatchNo = "batch_no";
    public const string FileSha256 = "file_sha256";
    public const string RowCount = "row_count";
    public const string CreateCount = "create_count";
    public const string UpdateCount = "update_count";
    public const string SkipCount = "skip_count";
    public const string RemovedCount = "removed_count";
}
