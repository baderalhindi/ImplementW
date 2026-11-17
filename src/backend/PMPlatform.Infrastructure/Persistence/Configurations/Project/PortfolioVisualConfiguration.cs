using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Project;

namespace PMPlatform.Infrastructure.Persistence.Configurations.Project;

internal sealed class PortfolioVisualConfiguration : IEntityTypeConfiguration<PortfolioVisual>
{
    public void Configure(EntityTypeBuilder<PortfolioVisual> builder)
    {
        builder.ToTable("portfolio_visual", "project", t =>
        {
            // The twelve-column grid: a visual starts inside it and ends by its last column.
            t.HasCheckConstraint(
                "ck_portfolio_visual_layout",
                "layout_column BETWEEN 0 AND 11 AND layout_width BETWEEN 1 AND 12 AND layout_column + layout_width <= 12 "
                + "AND layout_row >= 0 AND layout_height BETWEEN 1 AND 12");
            // Measures are codes from a fixed list, never an expression (BR-DSH-030).
            t.HasCheckConstraint(
                "ck_portfolio_visual_measures",
                "cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', "
                + "'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]");
            t.HasCheckConstraint("ck_portfolio_visual_sort_order", "sort_order >= 1");
        });
        builder.Property(e => e.TitleAr).HasMaxLength(100);
        builder.Property(e => e.TitleEn).HasMaxLength(100);
        builder.HasIndex(e => e.SortOrder).IsUnique();
    }
}
