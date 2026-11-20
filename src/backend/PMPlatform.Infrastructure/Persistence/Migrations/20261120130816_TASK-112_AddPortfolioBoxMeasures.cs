using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioBoxMeasures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_measures; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (the box sections' nine figures added), same release");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type <> 'SECTION' AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS', 'ON_TRACK', 'LATE', 'STALLED', 'NOT_MEASURED', 'FOLLOW_UP_STALLED', 'FOLLOW_UP_SUSPENDED', 'FOLLOW_UP_LATE', 'FOLLOW_UP_ACTION_OVERDUE', 'FOLLOW_UP_OPEN_OBSTACLE']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual");

            // A visual drawing one of the box figures cannot be kept under the older check: it goes (a container's children with it).
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE NOT (measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]);");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type <> 'SECTION' AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");
        }
    }
}
