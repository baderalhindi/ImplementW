using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualWidgets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX project.ix_portfolio_visual_section; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced below by the same unique index leaving out DASHBOARD_WIDGET (several per design), same release");

            migrationBuilder.Sql("ALTER TABLE project.portfolio_visual DROP CONSTRAINT ck_portfolio_visual_section; -- EXPAND-THEN-CONTRACT-REVIEWED: replaced by a widened check below (DASHBOARD_WIDGET added), same release");

            migrationBuilder.AddColumn<string>(
                name: "widget_code",
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
                filter: "section IS NOT NULL AND section <> 'DASHBOARD_WIDGET'");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_visual_widget_code",
                schema: "project",
                table: "portfolio_visual",
                column: "widget_code",
                unique: true,
                filter: "widget_code IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"section\" IN ('PORTFOLIO_TABLE', 'INITIATIVE_TABLE', 'SECTOR_TABLE', 'STATUS_TABLE', 'CURRENT_STAGE_TABLE', 'ON_PLAN_COUNTS', 'FOLLOW_UP_REASONS', 'TOP_FOLLOW_UP', 'ENTITY_COMPLETION', 'PROJECT_LOCATIONS', 'DASHBOARD_WIDGETS', 'ENTITY_COMPLETION_TABLE', 'DASHBOARD_WIDGET')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_widget_code",
                schema: "project",
                table: "portfolio_visual",
                sql: "COALESCE(section = 'DASHBOARD_WIDGET', FALSE) = (widget_code IS NOT NULL) AND (widget_code IS NULL OR widget_code ~ '^[A-Za-z0-9_.-]+$')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The release before this one cannot draw a single widget, and its section check and unique index refuse one: such visuals go with the column.
            migrationBuilder.Sql("DELETE FROM project.portfolio_visual WHERE section = 'DASHBOARD_WIDGET';");

            migrationBuilder.DropIndex(
                name: "ix_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropIndex(
                name: "ix_portfolio_visual_widget_code",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_widget_code",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "widget_code",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual",
                column: "section",
                unique: true,
                filter: "section IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_section",
                schema: "project",
                table: "portfolio_visual",
                sql: "\"section\" IN ('PORTFOLIO_TABLE', 'INITIATIVE_TABLE', 'SECTOR_TABLE', 'STATUS_TABLE', 'CURRENT_STAGE_TABLE', 'ON_PLAN_COUNTS', 'FOLLOW_UP_REASONS', 'TOP_FOLLOW_UP', 'ENTITY_COMPLETION', 'PROJECT_LOCATIONS', 'DASHBOARD_WIDGETS', 'ENTITY_COMPLETION_TABLE')");
        }
    }
}
