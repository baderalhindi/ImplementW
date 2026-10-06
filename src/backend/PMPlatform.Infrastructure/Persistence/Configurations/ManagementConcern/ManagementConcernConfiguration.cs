using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;
using ProjectEntity = PMPlatform.Domain.Project.Project;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ManagementConcern;

internal sealed class ManagementConcernConfiguration : IEntityTypeConfiguration<ConcernEntity>
{
    public void Configure(EntityTypeBuilder<ConcernEntity> builder)
    {
        builder.ToTable("management_concern", "management_concern");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");
        builder.HasNarrative(e => e.Resolution, "resolution");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);

        // TASK-026 (indexing-strategy.md I-16 to I-19): SCR-083 and SCR-085 in the project workspace and under every scope, their
        // status filter, and a person's assigned concerns; and the issue side of edge 15 (ERD §5.10: "queryable from both sides").
        builder.HasIndex(e => new { e.ProjectId, e.ConcernType, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.ConcernType, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.ConcernType, e.Status, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.AssigneeUserId, e.Status });
        builder.HasIndex(e => e.OriginatingRiskId);

        builder.HasCheck("revision_no", "revision_no >= 1");
        builder.HasCheck("overall_impact_level", "overall_impact_level BETWEEN 1 AND 5");

        // A severity is computed with the version whose rule computed it, and from an overall level: all three, or none.
        builder.HasCheck("severity",
            "(severity_item_id IS NULL) = (severity_configuration_version_id IS NULL) AND (severity_item_id IS NULL) = (overall_impact_level IS NULL)");

        // Past OPEN a concern has an assignee; from PENDING_VALIDATION on, a resolution; RESOLVED and CLOSED when they happened.
        builder.HasCheck("assigned", "status = 'OPEN' OR assignee_user_id IS NOT NULL");
        builder.HasCheck("resolution", "status IN ('OPEN', 'ASSIGNED', 'IN_PROGRESS') OR resolution IS NOT NULL");
        builder.HasCheck("resolved", "(status IN ('RESOLVED', 'CLOSED')) = (resolved_at IS NOT NULL)");
        builder.HasCheck("closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.CategoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.PriorityItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.SeverityItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.SeverityConfigurationVersionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_management_concern_severity_configuration_version");
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RaisedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AssigneeUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RiskEntity>().WithMany().HasForeignKey(e => e.OriginatingRiskId).OnDelete(DeleteBehavior.Restrict);
    }
}
