using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualSections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_layout; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below, same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_measures; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below, same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_visual_type; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below, same release");

            migrationBuilder.AddColumn<string>(
                name: "section",
                schema: "project",
                table: "portfolio_visual",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual",
                column: "section",
                unique: true,
                filter: "section IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_layout",
                schema: "project",
                table: "portfolio_visual",
                sql: "layout_column BETWEEN 0 AND 11 AND layout_width BETWEEN 1 AND 12 AND layout_column + layout_width <= 12 AND layout_row >= 0 AND layout_height BETWEEN 1 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "(visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE') OR (visual_type <> 'SECTION' AND section IS NULL AND cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[])");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"section\" IN ('PORTFOLIO_TABLE', 'INITIATIVE_TABLE', 'SECTOR_TABLE', 'STATUS_TABLE', 'CURRENT_STAGE_TABLE', 'ON_PLAN_COUNTS', 'FOLLOW_UP_REASONS', 'TOP_FOLLOW_UP', 'ENTITY_COMPLETION', 'PROJECT_LOCATIONS', 'DASHBOARD_WIDGETS', 'ENTITY_COMPLETION_TABLE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_layout",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "section",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_layout",
                schema: "project",
                table: "portfolio_visual",
                sql: "layout_column BETWEEN 0 AND 11 AND layout_width BETWEEN 1 AND 12 AND layout_column + layout_width <= 12 AND layout_row >= 0 AND layout_height BETWEEN 1 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE')");
        }
    }
}
