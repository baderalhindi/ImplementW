using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_measures; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (a MAP visual with one measure or a value), same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_visual_type; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (MAP added), same release");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type = 'TEXT' AND cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type = 'MAP' AND section IS NULL AND group_by IN ('CITY', 'REGION') AND data_fields IS NOT NULL AND (cardinality(measures) = 0 OR (cardinality(measures) = 1 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]))) OR (visual_type NOT IN ('SECTION', 'TEXT', 'MAP') AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER', 'TEXT', 'MAP')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The release before this one has no map visual, and its checks refuse one: each goes before they are restored.
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE visual_type = 'MAP';");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type = 'TEXT' AND cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type NOT IN ('SECTION', 'TEXT') AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER', 'TEXT')");
        }
    }
}
