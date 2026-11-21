using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualHiddenColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "hidden_columns",
                schema: "project",
                table: "portfolio_visual",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_hidden_columns",
                schema: "project",
                table: "portfolio_visual",
                sql: "hidden_columns IS NULL OR (visual_type = 'SECTION' AND section IN ('PORTFOLIO_TABLE', 'INITIATIVE_TABLE', 'SECTOR_TABLE', 'STATUS_TABLE', 'CURRENT_STAGE_TABLE', 'TOP_FOLLOW_UP', 'ENTITY_COMPLETION_TABLE') AND cardinality(hidden_columns) BETWEEN 1 AND 20)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_hidden_columns",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "hidden_columns",
                schema: "project",
                table: "portfolio_visual");
        }
    }
}
