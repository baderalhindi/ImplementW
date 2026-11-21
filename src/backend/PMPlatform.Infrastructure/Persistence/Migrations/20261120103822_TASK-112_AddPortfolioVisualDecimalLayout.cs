using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualDecimalLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "layout_width",
                schema: "project",
                table: "portfolio_visual",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<decimal>(
                name: "layout_row",
                schema: "project",
                table: "portfolio_visual",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<decimal>(
                name: "layout_height",
                schema: "project",
                table: "portfolio_visual",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");

            migrationBuilder.AlterColumn<decimal>(
                name: "layout_column",
                schema: "project",
                table: "portfolio_visual",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: false,
                oldClrType: typeof(short),
                oldType: "smallint");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A type change keeps a CHECK by rewriting it with casts (layout_height >= 2::numeric would read layout_height::numeric >= 2::numeric on
            // smallint), so the two CHECKs on the layout columns are dropped and added again with their own text.
            //
            // Back to whole cells: each value is rounded down, so a column plus its width stays within 12, a width and a height at least 1 and a container
            // at least 2 rows tall. Two decimal visuals that touched may then overlap by up to a cell; the database never checks overlaps, and the design
            // service refuses that design on its next save until they are moved apart.
            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_layout",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_container",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.Sql(
                "ALTER TABLE project.portfolio_visual "
                + "ALTER COLUMN layout_column TYPE smallint USING floor(layout_column)::smallint, "
                + "ALTER COLUMN layout_row TYPE smallint USING floor(layout_row)::smallint, "
                + "ALTER COLUMN layout_width TYPE smallint USING floor(layout_width)::smallint, "
                + "ALTER COLUMN layout_height TYPE smallint USING floor(layout_height)::smallint;");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_layout",
                schema: "project",
                table: "portfolio_visual",
                sql: "layout_column BETWEEN 0 AND 11 AND layout_width BETWEEN 1 AND 12 AND layout_column + layout_width <= 12 AND layout_row >= 0 AND layout_height BETWEEN 1 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_container",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type <> 'CONTAINER' OR (cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL AND axes IS NULL "
                + "AND widget_code IS NULL AND layout_height >= 2 AND parent_id IS NULL)");
        }
    }
}
