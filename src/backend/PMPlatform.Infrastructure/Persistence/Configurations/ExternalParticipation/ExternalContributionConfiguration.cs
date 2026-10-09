using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.IdentityAccess;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ExternalParticipation;

internal sealed class ExternalContributionConfiguration : IEntityTypeConfiguration<ExternalContribution>
{
    public void Configure(EntityTypeBuilder<ExternalContribution> builder)
    {
        builder.ToTable("external_contribution", "external_participation");
        builder.HasRowVersion();
        builder.HasNarrative(e => e.ReviewReason, "review_reason");
        builder.HasNarrative(e => e.ReviewInternalNote, "review_internal_note");
        builder.Property(e => e.RevisionNo).HasDatabaseDefault(1);
        builder.Property(e => e.TargetState).HasMaxLength(50);

        // A request's revisions are numbered from 1, whoever answers (the ERD's key, without the contributor: a reassigned responder continues
        // the numbering); a returned revision is corrected by one revision only.
        builder.HasIndex(e => new { e.ExternalUpdateRequestId, e.RevisionNo }).IsUnique();
        builder.HasIndex(e => e.PreviousRevisionId).IsUnique();
        builder.HasIndex(e => e.ReviewedByUserId);

        // TASK-026 (indexing-strategy.md I-43, I-44): SCR-164 a contributor's revisions; SCR-165 the review queue by status.
        builder.HasIndex(e => new { e.ContributorUserId, e.SubmittedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.SubmittedAt, e.Id });

        builder.HasCheck("revision_no", "revision_no >= 1 AND (revision_no = 1) = (previous_revision_id IS NULL)");

        // A DRAFT is a revision never submitted; review starts after submission and is decided once, by its reviewer; a return or a
        // rejection says why. A submitted revision of a typed source keeps the version and state it was answered against.
        builder.HasCheck("submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (target_version IS NULL) = (target_state IS NULL)");
        builder.HasCheck("review_started", "(status IN ('DRAFT', 'SUBMITTED')) = (review_started_at IS NULL)");
        builder.HasCheck("reviewed",
            "(status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW')) = (reviewed_at IS NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
        builder.HasCheck("review_reason", "status NOT IN ('RETURNED', 'REJECTED') OR review_reason IS NOT NULL");

        builder.HasOne<ExternalUpdateRequest>().WithMany().HasForeignKey(e => e.ExternalUpdateRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalContribution>().WithMany().HasForeignKey(e => e.PreviousRevisionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectEntity>().WithMany().HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ExternalEntity>().WithMany().HasForeignKey(e => e.ExternalEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ContributorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
