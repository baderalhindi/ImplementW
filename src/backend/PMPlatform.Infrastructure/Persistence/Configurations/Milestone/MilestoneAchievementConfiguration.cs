using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Milestone;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Milestone;

internal sealed class MilestoneAchievementConfiguration : IEntityTypeConfiguration<MilestoneAchievement>
{
    /// <summary>The ERD's invariant (TASK-050): at most one ACCEPTED revision per milestone, the current one, for any writer.</summary>
    public const string SingleAcceptedKey = "ix_milestone_achievement_accepted_project_milestone_id";

    /// <summary>At most one revision per milestone on its way, DRAFT or SUBMITTED, for any writer (milestone-achievement.md D-6).</summary>
    public const string SingleOpenKey = "ix_milestone_achievement_open_project_milestone_id";

    public void Configure(EntityTypeBuilder<MilestoneAchievement> builder)
    {
        builder.ToTable("milestone_achievement", "milestone");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.HasNarrative(e => e.Narrative, "narrative");
        builder.HasNarrative(e => e.ReturnReason, "return_reason");
        builder.HasIndex(e => new { e.ProjectMilestoneId, e.RevisionNo }).IsUnique();
        builder.HasIndex(e => e.ProjectMilestoneId, SingleAcceptedKey).IsUnique().HasFilter("status = 'ACCEPTED'").HasDatabaseName(SingleAcceptedKey);
        builder.HasIndex(e => e.ProjectMilestoneId, SingleOpenKey).IsUnique().HasFilter("status IN ('DRAFT', 'SUBMITTED')").HasDatabaseName(SingleOpenKey);

        builder.HasCheck("revision_no", "revision_no >= 1");

        // The workflow's facts: submitted unless DRAFT; reviewed once WF-11's outcome applied; the accepted date exactly while
        // ACCEPTED or SUPERSEDED; superseded by another revision exactly while SUPERSEDED; a return reason only on a RETURNED one.
        builder.HasCheck(
            "submitted",
            "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
        builder.HasCheck(
            "reviewed",
            "(status IN ('RETURNED', 'ACCEPTED', 'SUPERSEDED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
        builder.HasCheck("accepted", "(status IN ('ACCEPTED', 'SUPERSEDED')) = (accepted_actual_achievement_date IS NOT NULL)");
        builder.HasCheck(
            "superseded",
            "(status = 'SUPERSEDED') = (superseded_by_achievement_id IS NOT NULL) AND (superseded_by_achievement_id IS NULL OR superseded_by_achievement_id <> id)");
        builder.HasCheck("return_reason", "return_reason IS NULL OR status = 'RETURNED'");

        builder.HasOne<MilestoneAchievement>().WithMany().HasForeignKey(e => e.SupersededByAchievementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectMilestone>().WithMany().HasForeignKey(e => e.ProjectMilestoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);
    }
}
