using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Common;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batch", "common");
        builder.IsAppendOnly();
        builder.Property(e => e.RegisterCode).HasMaxLength(50);
        builder.Property(e => e.FileName).HasMaxLength(255);
        builder.Property(e => e.FileSha256).HasColumnType("char(64)");

        // The batches of a register, newest first.
        builder.HasIndex(e => new { e.RegisterCode, e.CreatedAt });
    }
}
