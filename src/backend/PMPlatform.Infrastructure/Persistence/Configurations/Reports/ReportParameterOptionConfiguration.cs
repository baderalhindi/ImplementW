using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Reports;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportParameterOptionConfiguration : IEntityTypeConfiguration<ReportParameterOption>
{
    public void Configure(EntityTypeBuilder<ReportParameterOption> builder)
    {
        builder.ToTable("report_parameter_option", "reports");
        builder.Property(e => e.ValueCode).HasMaxLength(50);
        builder.HasBilingualLabel(e => e.Label, "label");
        builder.Property(e => e.CatalogueEntryReference).HasMaxLength(50);
        builder.HasIndex(e => new { e.ReportParameterId, e.ValueCode }).IsUnique();

        builder.HasCheck("value_code", "value_code ~ '^[A-Z][A-Z0-9_]*$'");

        // An FG-02 §5.1 catalogue code (ADR-006 MAPPED), e.g. RPT-SUS-001.
        builder.HasCheck("catalogue_entry_reference", "catalogue_entry_reference ~ '^RPT-[A-Z]{3}-[0-9]{3}$'");

        builder.HasOne<ReportParameter>().WithMany().HasForeignKey(e => e.ReportParameterId).OnDelete(DeleteBehavior.Cascade);
    }
}
