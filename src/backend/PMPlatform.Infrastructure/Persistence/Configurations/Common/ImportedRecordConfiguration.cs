using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Common;

internal sealed class ImportedRecordConfiguration : IEntityTypeConfiguration<ImportedRecord>
{
    public void Configure(EntityTypeBuilder<ImportedRecord> builder)
    {
        builder.ToTable("imported_record", "common");
        builder.IsAppendOnly();
        builder.Property(e => e.RegisterCode).HasMaxLength(50);
        builder.Property(e => e.LegacyId).HasMaxLength(100);

        // BE-027: one platform identity per legacy record, however often the workbook is loaded.
        builder.HasIndex(e => new { e.RegisterCode, e.LegacyId }).IsUnique();
        builder.HasIndex(e => e.ImportBatchId);

        builder.HasOne<ImportBatch>().WithMany().HasForeignKey(e => e.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
    }
}
