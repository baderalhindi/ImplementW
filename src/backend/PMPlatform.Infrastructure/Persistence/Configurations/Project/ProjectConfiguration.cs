using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("project", "project");
        builder.HasRowVersion();
        builder.Property(e => e.FormalProjectId).HasMaxLength(50);
        builder.HasIndex(e => e.FormalProjectId).IsUnique();
        builder.HasNarrative(e => e.Title, "title");
        builder.Property(e => e.TitleEn).HasMaxLength(2000);
        builder.HasNarrative(e => e.Description, "description");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.GovernanceProfileOverridden).HasDatabaseDefault(false);
        builder.HasNarrative(e => e.GovernanceProfileOverrideReason, "governance_profile_override_reason");
        builder.Property(e => e.Latitude).HasPrecision(18, 4);
        builder.Property(e => e.Longitude).HasPrecision(18, 4);

        // The 2 Nov 2026 review (R3-3): strategy classification, legacy reference and RPMO's follow-up.
        // The 5 Nov 2026 review (R4-5): the seed marker the reset command deletes by; only demonstration data carries one.
        builder.Property(e => e.SeedSource).HasMaxLength(20);
        builder.HasCheck("seed_source", "seed_source IS NULL OR seed_source = 'DEMO'");
        builder.Property(e => e.LegacyReference).HasMaxLength(100);
        builder.HasIndex(e => e.LegacyReference).IsUnique().HasFilter("legacy_reference IS NOT NULL");
        builder.HasNarrative(e => e.CurrentObstacle, "current_obstacle");
        builder.HasNarrative(e => e.NextAction, "next_action");
        builder.Property(e => e.NextActionOwner).HasMaxLength(200);
        builder.Property(e => e.DeclaredPlannedPercent).HasPrecision(18, 4);
        builder.Property(e => e.DeclaredActualPercent).HasPrecision(18, 4);
        builder.Property(e => e.DeclaredPlannedSpendPercent).HasPrecision(18, 4);
        builder.Property(e => e.DeclaredActualSpendPercent).HasPrecision(18, 4);
        builder.HasCheck(
            "declared_percent_range",
            "(declared_planned_percent IS NULL OR declared_planned_percent BETWEEN 0 AND 100) AND (declared_actual_percent IS NULL OR declared_actual_percent BETWEEN 0 AND 100)"
            + " AND (declared_planned_spend_percent IS NULL OR declared_planned_spend_percent BETWEEN 0 AND 100)"
            + " AND (declared_actual_spend_percent IS NULL OR declared_actual_spend_percent BETWEEN 0 AND 100)");

        // TASK-041: the formal identifier is issued when AHDA approves, so every state from APPROVED_PLANNED on has one.
        builder.HasCheck(
            "formal_project_id",
            "lifecycle_state IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED') OR formal_project_id IS NOT NULL");

        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ClassificationItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ExternalEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ProjectManagerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.GovernanceProfileItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.RegionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.CityItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.StrategicPortfolioItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.StrategicInitiativeItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.PriorityItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.FollowUpOwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.EntityContactUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectImportBatch>().WithMany().HasForeignKey(e => e.ProjectImportBatchId).OnDelete(DeleteBehavior.Restrict);

        // TASK-026 (indexing-strategy.md I-01 to I-05): SCR-025/SCR-026 sort by updated_at desc, id desc under each
        // data scope, read by a backward index scan. The scope composites lead with their foreign key, so they replace
        // its single-column index.
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.LifecycleState, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.DepartmentId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.ProjectManagerUserId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.ExternalEntityId, e.UpdatedAt, e.Id });
    }
}
