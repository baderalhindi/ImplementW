namespace PMPlatform.Application.Features.ProjectTask.Contracts.Events;

/// <summary>The attribute names of ProjectTask's audit events.</summary>
public static class ProjectTaskAuditAttributes
{
    public const string ScheduleActivityId = "schedule_activity_id";
    public const string ParentTaskId = "parent_task_id";
    public const string Title = "title";
    public const string Description = "description";
    public const string AssigneeUserId = "assignee_user_id";
    public const string PriorityItemId = "priority_item_id";
    public const string PlannedStartDate = "planned_start_date";
    public const string PlannedFinishDate = "planned_finish_date";
    public const string PlannedDurationDays = "planned_duration_days";
    public const string Status = "status";
    public const string ActualStartDate = "actual_start_date";
    public const string ActualFinishDate = "actual_finish_date";
    public const string ActualPercentComplete = "actual_percent_complete";
    public const string BlockedReason = "blocked_reason";
    public const string ReopenedCount = "reopened_count";
    public const string PredecessorTaskId = "predecessor_task_id";
    public const string SuccessorTaskId = "successor_task_id";
    public const string DependencyType = "dependency_type";
    public const string Source = "source";
    public const string ExternalEntityId = "external_entity_id";
    public const string ExternalContributionId = "external_contribution_id";
    public const string ContributionRevisionNo = "contribution_revision_no";
    public const string SourceApplicationId = "source_application_id";
}
