using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.DocumentManagement;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence.Configurations.DocumentManagement;

internal sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    /// <summary>Two versions of one document added at once: the second loses.</summary>
    public const string NumberKey = "ix_document_version_document_id_version_no";

    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_version", "document_management");
        builder.Property(e => e.StorageObjectKey).HasMaxLength(500);
        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.ContentType).HasMaxLength(200);
        builder.Property(e => e.ChecksumSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(e => e.ScanReference).HasMaxLength(200);
        builder.HasCheck("version_no", "version_no >= 1");
        builder.HasCheck("size_bytes", "size_bytes >= 0");
        builder.HasCheck("checksum_sha256", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
        builder.HasCheck("scan_completed", "(scan_state = 'SCAN_PENDING') = (scan_completed_at IS NULL)");

        builder.HasIndex(e => e.StorageObjectKey).IsUnique();
        builder.HasIndex(e => new { e.DocumentId, e.VersionNo }).IsUnique().HasDatabaseName(NumberKey);

        // The scan worker's queue, least recently tried first: a worker query, not a register (indexing-strategy.md §2).
        builder.HasIndex(e => e.UpdatedAt).HasFilter("scan_state = 'SCAN_PENDING'").HasDatabaseName("ix_document_version_scan_queue");

        builder.HasOne<Document>().WithMany().HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Two scan passes deciding one version at once cannot both commit.
        builder.HasRowVersion();
    }
}
