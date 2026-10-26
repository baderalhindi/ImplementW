using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.MasterDataConfig;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ExternalParticipation;

internal sealed class ExternalUpdateRequestConfiguration : IEntityTypeConfiguration<ExternalUpdateRequest>
{
    public void Configure(EntityTypeBuilder<ExternalUpdateRequest> builder)
    {
        builder.ToTable("external_update_request", "external_participation");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.Instructions, "instructions");
        builder.HasNarrative(e => e.CancellationReason, "cancellation_reason");
        builder.Property(e => e.ContributionSchemaCode).HasMaxLength(100);
        builder.Property(e => e.TargetModule).HasMaxLength(50);
        builder.Property(e => e.TargetType).HasMaxLength(100);

        // TASK-026 (indexing-strategy.md I-41, I-42): SCR-163 an entity's requests, due first; SCR-160 a project's, by issue. The register's
        // default order, most recently changed first (P-2), by project and under ALL scope; a person's (the responder's inbox, the reviewer's queue).
        builder.HasIndex(e => new { e.ExternalEntityId, e.Status, e.DueDate });
        builder.HasIndex(e => new { e.ProjectId, e.IssuedAt, e.Id });
        builder.HasIndex(e => new { e.ProjectId, e.UpdatedAt, e.Id });
        builder.HasIndex(e => new { e.UpdatedAt, e.Id });
        builder.HasIndex(e => e.ResponsibleUserId);
        builder.HasIndex(e => e.ReviewerUserId);

        // A request past DRAFT was issued, by someone, to a named responder with a named reviewer, under the PARTICIPATION version that
        // enabled it; it is cancelled exactly when it says why, and closed exactly when it says when. Its source is all three columns or none.
        builder.HasCheck("issued",
            "(status = 'DRAFT') = (issued_at IS NULL) AND (issued_at IS NULL) = (issued_by_user_id IS NULL) AND (status = 'DRAFT') = (participation_configuration_version_id IS NULL)");
        builder.HasCheck("people", "status = 'DRAFT' OR (responsible_user_id IS NOT NULL AND reviewer_user_id IS NOT NULL)");
        builder.HasCheck("cancelled", "(status = 'CANCELLED') = (cancelled_at IS NOT NULL) AND (cancelled_at IS NULL) = (cancellation_reason IS NULL)");
        builder.HasCheck("closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");
        builder.HasCheck("target", "(target_id IS NULL) = (target_type IS NULL) AND (target_id IS NULL) = (target_module IS NULL)");

        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ExternalEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.ContributionTypeItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ParticipationConfigurationVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ResponsibleUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
