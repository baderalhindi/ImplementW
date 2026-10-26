using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportOutputContentConfiguration : IEntityTypeConfiguration<ReportOutputContent>
{
    public void Configure(EntityTypeBuilder<ReportOutputContent> builder)
    {
        builder.ToTable("report_output_content", "reports");
        builder.Property(e => e.StorageObjectKey).HasMaxLength(500);
        builder.HasIndex(e => e.StorageObjectKey).IsUnique();

        // Bytes belong to an output: stored under its key and deleted when it is purged; an output's key never names bytes of another.
        builder.HasOne<GeneratedOutput>().WithOne().HasForeignKey<ReportOutputContent>(e => e.StorageObjectKey)
            .HasPrincipalKey<GeneratedOutput>(e => e.StorageObjectKey).OnDelete(DeleteBehavior.Restrict);
    }
}
