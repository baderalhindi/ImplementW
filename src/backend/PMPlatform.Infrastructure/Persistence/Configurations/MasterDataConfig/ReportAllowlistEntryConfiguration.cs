using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.MasterDataConfig;

internal sealed class ReportAllowlistEntryConfiguration : IEntityTypeConfiguration<ReportAllowlistEntry>
{
    public void Configure(EntityTypeBuilder<ReportAllowlistEntry> builder)
    {
        builder.ToTable("report_allowlist_entry", "master_data_config");
        builder.Property(e => e.SourceEntityCode).HasMaxLength(100);
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.ConfigurationVersionId, e.SourceEntityCode, e.FieldCode }).IsUnique();
        builder.HasBilingualLabel(e => e.Label, "label");

        builder.HasOne<ConfigurationVersion>().WithMany().HasForeignKey(e => e.ConfigurationVersionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
