using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<ProjectEntity>
{
    public void Configure(EntityTypeBuilder<ProjectEntity> builder)
    {
        builder.ToTable("project", "project");
        builder.Property(e => e.FormalProjectId).HasMaxLength(50);
        builder.HasIndex(e => e.FormalProjectId).IsUnique();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.GovernanceProfileOverridden).HasDatabaseDefault(false);
        builder.HasNarrative(e => e.GovernanceProfileOverrideReason, "governance_profile_override_reason");
        builder.Property(e => e.Latitude).HasPrecision(18, 4);
        builder.Property(e => e.Longitude).HasPrecision(18, 4);

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
    }
}
