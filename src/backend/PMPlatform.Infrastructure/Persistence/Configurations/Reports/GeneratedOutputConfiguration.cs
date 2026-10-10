using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class GeneratedOutputConfiguration : IEntityTypeConfiguration<GeneratedOutput>
{
    public void Configure(EntityTypeBuilder<GeneratedOutput> builder)
    {
        builder.ToTable("generated_output", "reports");
        builder.Property(e => e.StorageObjectKey).HasMaxLength(500);
        builder.Property(e => e.FileName).HasMaxLength(500);
        builder.Property(e => e.ContentType).HasMaxLength(200);
        builder.Property(e => e.ChecksumSha256).HasColumnType("char(64)");
        builder.Property(e => e.AuthorizationFootprint).HasColumnType("jsonb");
        builder.HasIndex(e => e.ReportJobId).IsUnique();
        // storage_object_key is unique by the alternate key report_output_content's foreign key references (ak_generated_output_storage_object_key).

        // The expiry pass: outputs still on file, by expiry.
        builder.HasIndex(e => new { e.Status, e.ExpiresAt });

        // Opaque (GEN-008): a random key, never a path or a name.
        builder.HasCheck("storage_object_key", "storage_object_key ~ '^[0-9a-f]{32}$'");

        // Safe for a Content-Disposition header (US-RPT-SYS-058): no path, no control character.
        builder.HasCheck("file_name", "file_name ~ '^[A-Za-z0-9_-]+\\.(pdf|xlsx|csv)$'");
        builder.HasCheck("checksum_sha256", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
        builder.HasCheck("size_bytes", "size_bytes >= 0 AND row_count >= 0");
        builder.HasCheck("purged", "(status = 'PURGED') = (purged_at IS NOT NULL)");

        builder.HasOne<ReportJob>().WithOne().HasForeignKey<GeneratedOutput>(e => e.ReportJobId).OnDelete(DeleteBehavior.Restrict);
    }
}
