using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Progress;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Progress;

internal sealed class ProgressSubmissionConfiguration : IEntityTypeConfiguration<ProgressSubmission>
{
    public void Configure(EntityTypeBuilder<ProgressSubmission> builder)
    {
        builder.ToTable("progress_submission", "progress");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.ActualPercentCalculated).HasPrecision(18, 4);
        builder.Property(e => e.ActualPercentOverride).HasPrecision(18, 4);
        builder.Property(e => e.PlannedPercent).HasPrecision(18, 4);
        builder.HasNarrative(e => e.OverrideReason, "override_reason");
        builder.HasNarrative(e => e.Narrative, "narrative");
        builder.HasNarrative(e => e.ReturnReason, "return_reason");
        builder.HasIndex(e => new { e.ReportingCycleId, e.RevisionNo }).IsUnique();

        // TASK-026 (indexing-strategy.md I-37): SCR-070 Progress Update History and the SCR-048 Progress tab.
        builder.HasIndex(e => new { e.ProjectId, e.SubmittedAt });

        builder.HasCheck("revision_no", "revision_no >= 1");
        builder.HasCheck(
            "percent",
            "actual_percent_calculated BETWEEN 0 AND 100 AND (actual_percent_override IS NULL OR actual_percent_override BETWEEN 0 AND 100) "
            + "AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");

        // ADR-009: an override needs its recorded reason, and a reason is only ever an override's.
        builder.HasCheck("override", "(actual_percent_override IS NULL) = (override_reason IS NULL)");
        builder.HasCheck("planned_baseline", "planned_percent IS NULL OR baseline_id IS NOT NULL");

        // Who submitted and who decided are set by the transitions that make them so, and only then.
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
        builder.HasCheck("reviewed", "(status IN ('RETURNED', 'PUBLISHED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
        builder.HasCheck("return_reason", "(status = 'RETURNED') = (return_reason IS NOT NULL)");

        builder.HasOne<ReportingCycle>().WithMany().HasForeignKey(e => e.ReportingCycleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.BaselineId).OnDelete(DeleteBehavior.Restrict);
    }
}
