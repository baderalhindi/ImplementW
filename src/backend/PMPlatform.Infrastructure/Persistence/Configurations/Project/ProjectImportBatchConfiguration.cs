using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class ProjectImportBatchConfiguration : IEntityTypeConfiguration<ProjectImportBatch>
{
    public void Configure(EntityTypeBuilder<ProjectImportBatch> builder)
    {
        builder.ToTable("project_import_batch", "project");
        builder.HasRowVersion();
        builder.HasIndex(e => e.BatchNo).IsUnique();
        builder.Property(e => e.FileName).HasMaxLength(255);
        builder.Property(e => e.FileSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(e => e.Plan).HasColumnType("jsonb");
        builder.HasNarrative(e => e.SubmissionNote, "submission_note");
        builder.HasNarrative(e => e.ReturnReason, "return_reason");

        // R3-4: the person who submitted a batch never decides it.
        builder.HasCheck("decided_by", "decided_by_user_id IS NULL OR decided_by_user_id <> created_by");
        builder.HasCheck("decision", "(status = 'SUBMITTED') = (decided_at IS NULL) AND (status = 'RETURNED') = (return_reason IS NOT NULL)");

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RolledBackByUserId).OnDelete(DeleteBehavior.Restrict);

        // The batch list, newest first, and the approver's queue of SUBMITTED batches (indexing-strategy I-62, I-63).
        builder.HasIndex(e => new { e.CreatedAt, e.Id });
        builder.HasIndex(e => new { e.Status, e.CreatedAt, e.Id });
    }
}
