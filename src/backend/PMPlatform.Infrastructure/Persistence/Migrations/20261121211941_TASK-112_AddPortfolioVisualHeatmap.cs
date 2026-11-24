using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualHeatmap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_visual_type; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (HEATMAP added), same release");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER', 'TEXT', 'MAP', 'HEATMAP')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The release before this one has no heatmap, and its check refuses one: each goes before it is restored.
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE visual_type = 'HEATMAP';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER', 'TEXT', 'MAP')");
        }
    }
}
