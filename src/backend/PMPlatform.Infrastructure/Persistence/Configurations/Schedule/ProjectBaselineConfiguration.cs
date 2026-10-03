using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;
using PMPlatform.Domain.Schedule;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Schedule;

internal sealed class ProjectBaselineConfiguration : IEntityTypeConfiguration<ProjectBaseline>
{
    /// <summary>The TASK-046 invariant (ERD §5.6): at most one ACTIVE baseline per project, for any writer and any interleaving.</summary>
    public const string SingleActiveKey = "ix_project_baseline_active_project_id";

    public void Configure(EntityTypeBuilder<ProjectBaseline> builder)
    {
        builder.ToTable("project_baseline", "schedule");
        builder.HasRowVersion();
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.HasNarrative(e => e.DeclaredScope, "declared_scope");
        builder.HasIndex(e => new { e.ProjectId, e.VersionNo }).IsUnique();
        builder.HasIndex(e => e.ProjectId).IsUnique().HasFilter("status = 'ACTIVE'").HasDatabaseName(SingleActiveKey);

        // One Declared Baseline per intake, so a redelivered ProjectIntakeRecorded writes none (ADR-014).
        builder.HasIndex(e => e.ProjectIntakeId).IsUnique();

        builder.HasCheck("numbers", "version_no >= 1 AND revision_no >= 1");

        // A DECLARED baseline is the intake's, with its end date and no change authorisation; an APPROVED one has neither.
        builder.HasCheck(
            "declared",
            "(baseline_type = 'DECLARED') = (project_intake_id IS NOT NULL) AND (baseline_type = 'DECLARED') = (declared_end_date IS NOT NULL) "
            + "AND (baseline_type = 'DECLARED' OR declared_scope IS NULL) AND (baseline_type = 'APPROVED' OR change_authorization_id IS NULL)");
        builder.HasCheck("activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
        builder.HasCheck(
            "superseded",
            "(status = 'SUPERSEDED') = (superseded_at IS NOT NULL) AND (superseded_at IS NULL) = (superseded_by_baseline_id IS NULL) "
            + "AND (superseded_by_baseline_id IS NULL OR superseded_by_baseline_id <> id)");

        builder.HasOne<ProjectBaseline>().WithMany().HasForeignKey(e => e.SupersededByBaselineId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectIntake>().WithMany().HasForeignKey(e => e.ProjectIntakeId).OnDelete(DeleteBehavior.Restrict);

        // change_authorization_id references change_request.change_authorization, which TASK-060 creates; TASK-060 adds the key
        // (schedule-baseline.md F-1).
    }
}
