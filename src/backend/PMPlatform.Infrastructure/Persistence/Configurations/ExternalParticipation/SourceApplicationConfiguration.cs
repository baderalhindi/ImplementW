using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.ExternalParticipation;

internal sealed class SourceApplicationConfiguration : IEntityTypeConfiguration<SourceApplication>
{
    /// <summary>WF-13 EXT-CC-18, for any writer: a revision is applied at most once, however many attempts are made at it.</summary>
    public const string SingleAppliedKey = "ix_source_application_applied_external_contribution_id";

    public void Configure(EntityTypeBuilder<SourceApplication> builder)
    {
        builder.ToTable("source_application", "external_participation");
        builder.HasRowVersion();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(200);
        builder.Property(e => e.FailureCode).HasMaxLength(100);

        // ERD: attempts are numbered within their revision, and a key makes one attempt (R-37).
        builder.HasIndex(e => new { e.ExternalContributionId, e.AttemptNo }).IsUnique();
        builder.HasIndex(e => e.IdempotencyKey).IsUnique();
        builder.HasIndex(e => e.ExternalContributionId, SingleAppliedKey).IsUnique().HasFilter("status = 'APPLIED'").HasDatabaseName(SingleAppliedKey);

        // TASK-026 (indexing-strategy.md I-45): SCR-166 conflicts and failures by state.
        builder.HasIndex(e => new { e.Status, e.AttemptedAt });
        builder.HasIndex(e => e.AttemptedByUserId);
        builder.HasIndex(e => e.RevalidatedByUserId);

        // A CONFLICT says what it found and a FAILED attempt why; only a CONFLICT is revalidated, once, by someone, to a version.
        builder.HasCheck("attempt_no", "attempt_no >= 1 AND completed_at >= attempted_at");
        builder.HasCheck("outcome", "(status = 'CONFLICT') = (actual_target_revision_no IS NOT NULL) AND (status = 'FAILED') = (failure_code IS NOT NULL)");
        builder.HasCheck("revalidated",
            "(revalidated_at IS NULL OR status = 'CONFLICT') AND (revalidated_at IS NULL) = (revalidated_by_user_id IS NULL) AND (revalidated_at IS NULL) = (revalidated_target_revision_no IS NULL)");

        builder.HasOne<ExternalContribution>().WithMany().HasForeignKey(e => e.ExternalContributionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.AttemptedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RevalidatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
