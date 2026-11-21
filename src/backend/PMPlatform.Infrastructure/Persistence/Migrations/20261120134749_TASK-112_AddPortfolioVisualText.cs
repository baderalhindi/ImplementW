using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_measures; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (a TEXT visual draws no measure), same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_visual_type; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (TEXT added), same release");

            migrationBuilder.AddColumn<string>(
                name: "text_box",
                schema: "project",
                table: "portfolio_visual",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type = 'TEXT' AND cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type NOT IN ('SECTION', 'TEXT') AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_text_box",
                schema: "project",
                table: "portfolio_visual",
                sql: "(visual_type = 'TEXT') = (text_box IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER', 'TEXT')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The release before this one has no text box, and its checks refuse one: each goes before they are restored.
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE visual_type = 'TEXT';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_text_box",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "text_box",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type <> 'SECTION' AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER')");
        }
    }
}
