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
                + "AND layout_row >= 0 AND layout_height BETWEEN 1 AND 24");
            // Measures are codes from a fixed list, never an expression (BR-DSH-030); the box sections' figures joined it on 20 Nov 2026.
            // A section names one of the dashboard's own parts and no measure or data field; any other visual no section, and one to three
            // measures or, drawing the data catalogue's fields instead (18 Nov 2026), no measure and its data fields. A container is checked apart; a text box
            // (20 Nov 2026) draws no figure and names no field.
            t.HasCheckConstraint(
                "ck_portfolio_visual_measures",
                "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) "
                + "OR (visual_type = 'TEXT' AND cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL) "
                + "OR (visual_type NOT IN ('SECTION', 'TEXT') AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 "
                + "AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', "
                + "'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', "
                + "'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) "
                + "OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");
            // A container (19 Nov 2026) draws nothing of its own, has its title bar and a row, and is on the dashboard itself: one level only.
            t.HasCheckConstraint(
                "ck_portfolio_visual_container",
                "visual_type <> 'CONTAINER' OR (cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL AND axes IS NULL "
                + "AND widget_code IS NULL AND layout_height >= 2 AND parent_id IS NULL)");
            // A text box, and only a text box, says something of its own (20 Nov 2026).
            t.HasCheckConstraint("ck_portfolio_visual_text_box", "(visual_type = 'TEXT') = (text_box IS NOT NULL)");
            t.HasCheckConstraint("ck_portfolio_visual_sort_order", "sort_order >= 1");
            // Only a bar, column or line chart has axes to set (19 Nov 2026).
            t.HasCheckConstraint("ck_portfolio_visual_axes", "axes IS NULL OR visual_type IN ('BAR', 'COLUMN', 'LINE')");
            // A DASHBOARD_WIDGET section names the governed widget it draws, and no other visual names one (19 Nov 2026). COALESCE: a visual with no section
            // compares as false, not null, so it may not carry a code.
            t.HasCheckConstraint(
                "ck_portfolio_visual_widget_code",
                "COALESCE(section = 'DASHBOARD_WIDGET', FALSE) = (widget_code IS NOT NULL) AND (widget_code IS NULL OR widget_code ~ '^[A-Za-z0-9_.-]+$')");
            // Only a table section hides columns, and it hides at least one when it names any (20 Nov 2026 table columns).
            t.HasCheckConstraint(
                "ck_portfolio_visual_hidden_columns",
                "hidden_columns IS NULL OR (visual_type = 'SECTION' AND section IN ('PORTFOLIO_TABLE', 'INITIATIVE_TABLE', 'SECTOR_TABLE', 'STATUS_TABLE', "
                + "'CURRENT_STAGE_TABLE', 'TOP_FOLLOW_UP', 'ENTITY_COMPLETION_TABLE') AND cardinality(hidden_columns) BETWEEN 1 AND 20)");
        });
        builder.Property(e => e.TitleAr).HasMaxLength(100);
        builder.Property(e => e.TitleEn).HasMaxLength(100);
        builder.Property(e => e.DataFields).HasColumnType("jsonb");
        builder.Property(e => e.Axes).HasColumnType("jsonb");
        builder.Property(e => e.TextBox).HasColumnType("jsonb");
        builder.Property(e => e.WidgetCode).HasMaxLength(50);
        // Decimal places in steps of 0.1 (20 Nov 2026); ck_portfolio_visual_layout keeps its ranges on them.
        builder.Property(e => e.LayoutColumn).HasPrecision(4, 1);
        builder.Property(e => e.LayoutRow).HasPrecision(4, 1);
        builder.Property(e => e.LayoutWidth).HasPrecision(4, 1);
        builder.Property(e => e.LayoutHeight).HasPrecision(4, 1);
        builder.HasIndex(e => e.SortOrder).IsUnique();
        // A child is in its container (19 Nov 2026) and goes with it; the service checks that the parent is a container.
        builder.HasOne<PortfolioVisual>().WithMany().HasForeignKey(e => e.ParentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => e.ParentId);
        // A section is placed once; DASHBOARD_WIDGET once per widget (19 Nov 2026).
        builder.HasIndex(e => e.Section).IsUnique().HasFilter("section IS NOT NULL AND section <> 'DASHBOARD_WIDGET'");
        builder.HasIndex(e => e.WidgetCode).IsUnique().HasFilter("widget_code IS NOT NULL");
    }
}
