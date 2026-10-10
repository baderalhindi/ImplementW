using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.MasterDataConfig;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportColumnConfiguration : IEntityTypeConfiguration<ReportColumn>
{
    public void Configure(EntityTypeBuilder<ReportColumn> builder)
    {
        builder.ToTable("report_column", "reports");
        builder.Property(e => e.SourceEntityCode).HasMaxLength(100);
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.HasIndex(e => new { e.ReportDefinitionId, e.SourceEntityCode, e.FieldCode }).IsUnique();
        builder.HasIndex(e => new { e.ReportDefinitionId, e.SortOrder }).IsUnique();

        // Codes, never expressions: a column names a field the register has, and the application refuses any other at validation.
        builder.HasCheck("source_entity_code", "source_entity_code ~ '^[A-Z][A-Z0-9_]*$'");
        builder.HasCheck("field_code", "field_code ~ '^[A-Z][A-Z0-9_]*$'");
        builder.HasCheck("sort_order", "sort_order >= 1");

        builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(e => e.ReportDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
