using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Risk;

internal sealed class RiskConfiguration : IEntityTypeConfiguration<RiskEntity>
{
    public void Configure(EntityTypeBuilder<RiskEntity> builder)
    {
        builder.ToTable("risk", "risk");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Title, "title");
        builder.HasNarrative(e => e.Description, "description");
        builder.HasNarrative(e => e.ClosureRationale, "closure_rationale");
        builder.Property(e => e.ReopenedCount).HasDatabaseDefault(0);

        // TASK-026 (indexing-strategy.md I-11 to I-15): SCR-080 in the project workspace and under every scope, its status, owner
        // and next-review filters, and review reminders.
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.OwnerUserId, e.Status });
        builder.HasIndex(e => e.NextReviewDate);

        builder.HasCheck("reopened_count", "reopened_count >= 0");

        // A risk is CLOSED exactly while it has its rationale, its closer and the moment it was closed.
        builder.HasCheck("closed",
            "(status = 'CLOSED') = (closed_at IS NOT NULL) AND (status = 'CLOSED') = (closed_by_user_id IS NOT NULL) AND (status = 'CLOSED') = (closure_rationale IS NOT NULL)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.RiskCategoryItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ClosedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
