using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportParameterConfiguration : IEntityTypeConfiguration<ReportParameter>
{
    public void Configure(EntityTypeBuilder<ReportParameter> builder)
    {
        builder.ToTable("report_parameter", "reports");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.Property(e => e.SourceEntityCode).HasMaxLength(100);
        builder.Property(e => e.FieldCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.ReportDefinitionId, e.Code }).IsUnique();

        builder.HasCheck("code", "code ~ '^[A-Z][A-Z0-9_]*$'");
        builder.HasCheck("sort_order", "sort_order >= 1");

        // An OPTION parameter, and only one, is bound to the field it filters.
        builder.HasCheck(
            "binding",
            "(data_type = 'OPTION') = (source_entity_code IS NOT NULL AND field_code IS NOT NULL) AND (source_entity_code IS NULL) = (field_code IS NULL)");

        builder.HasOne<ReportDefinition>().WithMany().HasForeignKey(e => e.ReportDefinitionId).OnDelete(DeleteBehavior.Cascade);
    }
}
