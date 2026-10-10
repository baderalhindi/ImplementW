using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Dashboards;
using PMPlatform.Domain.MasterDataConfig;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Dashboards;

internal sealed class DashboardWidgetConfiguration : IEntityTypeConfiguration<DashboardWidget>
{
    public void Configure(EntityTypeBuilder<DashboardWidget> builder)
    {
        builder.ToTable("dashboard_widget", "dashboards");
        builder.Property(e => e.Code).HasMaxLength(50);
        builder.HasBilingualLabel(e => e.Title, "title");
        builder.Property(e => e.SourceProjectionCode).HasMaxLength(100);
        builder.HasIndex(e => new { e.DashboardDefinitionId, e.Code }).IsUnique();

        // Codes, never expressions (BR-DSH-030): a widget names a registered projection and the application refuses any other
        // name at validation; the column itself admits nothing that could be read as a query.
        builder.HasCheck("code", "code ~ '^[A-Z][A-Z0-9_]*$'");
        builder.HasCheck("source_projection_code", "source_projection_code ~ '^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*$'");

        // A twelve-column grid (FG-01 §15.1 desktop layout): the widget starts on the grid and ends by column 12.
        builder.HasCheck("layout", "layout_row >= 1 AND layout_column BETWEEN 1 AND 12 AND layout_span BETWEEN 1 AND 12 AND layout_column + layout_span <= 13");

        builder.HasOne<DashboardDefinition>().WithMany().HasForeignKey(e => e.DashboardDefinitionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MasterDataItem>().WithMany().HasForeignKey(e => e.DataClassificationItemId).OnDelete(DeleteBehavior.Restrict);
    }
}
