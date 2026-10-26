using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;
using ProjectTaskEntity = PMPlatform.Domain.ProjectTask.ProjectTask;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ProjectTask;

internal sealed class ProjectTaskConfiguration : IEntityTypeConfiguration<ProjectTaskEntity>
{
    public void Configure(EntityTypeBuilder<ProjectTaskEntity> builder)
    {
        builder.ToTable("project_task", "project_task");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");
        builder.HasNarrative(e => e.BlockedReason, "blocked_reason");
        builder.Property(e => e.ActualPercentComplete).HasPrecision(18, 4);
        builder.Property(e => e.ReopenedCount).HasDatabaseDefault(0);

        // TASK-026 (indexing-strategy.md I-33, I-34): My Tasks, Overdue and Updates Required for the caller; a project's tasks.
        builder.HasIndex(e => new { e.AssigneeUserId, e.Status, e.PlannedFinishDate });
        builder.HasIndex(e => new { e.ProjectId, e.Status, e.PlannedFinishDate });

        builder.HasCheck("hierarchy", "parent_task_id IS NULL OR parent_task_id <> id");

        // ERD §7 row 2: the stored weight is the planned span, both days included.
        builder.HasCheck("planned", "planned_finish_date >= planned_start_date AND planned_duration_days = planned_finish_date - planned_start_date + 1");
        builder.HasCheck("actual", "actual_finish_date IS NULL OR (actual_start_date IS NOT NULL AND actual_finish_date >= actual_start_date)");
        builder.HasCheck("percent", "actual_percent_complete IS NULL OR actual_percent_complete BETWEEN 0 AND 100");
        builder.HasCheck("reopened_count", "reopened_count >= 0");

        // The state machine's facts: a task has started unless it is NOT_STARTED, is BLOCKED exactly while it has a reason, and is
        // COMPLETED exactly while it has its completion and actual finish.
        builder.HasCheck("started", "(status = 'NOT_STARTED' AND actual_start_date IS NULL) OR (status IN ('IN_PROGRESS', 'COMPLETED') AND actual_start_date IS NOT NULL) OR status IN ('BLOCKED', 'CANCELLED')");
        builder.HasCheck("blocked", "(status = 'BLOCKED') = (blocked_reason IS NOT NULL)");
        builder.HasCheck("completed", "(status = 'COMPLETED') = (completed_at IS NOT NULL) AND (status = 'COMPLETED') = (actual_finish_date IS NOT NULL)");

        builder.HasOne<ProjectTaskEntity>().WithMany().HasForeignKey(e => e.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ScheduleActivity>().WithMany().HasForeignKey(e => e.ScheduleActivityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.PriorityItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
