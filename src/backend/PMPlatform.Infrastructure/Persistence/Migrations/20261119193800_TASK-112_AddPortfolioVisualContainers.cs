using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualContainers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_measures; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (a CONTAINER draws nothing), same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_visual_type; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (CONTAINER added), same release");

            migrationBuilder.AddColumn<Guid>(
                name: "parent_id",
                schema: "project",
                table: "portfolio_visual",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_visual_parent_id",
                schema: "project",
                table: "portfolio_visual",
                column: "parent_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_container",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type <> 'CONTAINER' OR (cardinality(measures) = 0 AND section IS NULL AND group_by = 'NONE' AND data_fields IS NULL AND axes IS NULL AND widget_code IS NULL AND layout_height >= 2 AND parent_id IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "visual_type = 'CONTAINER' OR (visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type <> 'SECTION' AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE', 'CONTAINER')");

            migrationBuilder.AddForeignKey(
                name: "fk_portfolio_visual_portfolio_visual_parent_id",
                schema: "project",
                table: "portfolio_visual",
                column: "parent_id",
                principalSchema: "project",
                principalTable: "portfolio_visual",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The release before this one has no container, and its checks refuse one: each goes, its children with it (ON DELETE CASCADE).
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE visual_type = 'CONTAINER';");

            migrationBuilder.DropForeignKey(
                name: "fk_portfolio_visual_portfolio_visual_parent_id",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropIndex(
                name: "ix_portfolio_visual_parent_id",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_container",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "parent_id",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_measures",
                schema: "project",
                table: "portfolio_visual",
                sql: "(visual_type = 'SECTION' AND cardinality(measures) = 0 AND section IS NOT NULL AND group_by = 'NONE' AND data_fields IS NULL) OR (visual_type <> 'SECTION' AND section IS NULL AND ((cardinality(measures) BETWEEN 1 AND 3 AND measures <@ ARRAY['PROJECTS', 'IN_PROGRESS', 'VALUE_SAR', 'AVERAGE_PLANNED', 'AVERAGE_ACTUAL', 'GAP_POINTS', 'NEEDING_FOLLOW_UP', 'OVERDUE_ACTIONS']::text[]) OR (cardinality(measures) = 0 AND data_fields IS NOT NULL)))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_visual_type",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"visual_type\" IN ('CARD', 'BAR', 'COLUMN', 'PIE', 'DONUT', 'TABLE', 'GAUGE', 'SECTION', 'LINE')");
        }
    }
}
