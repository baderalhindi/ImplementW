using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPortfolioVisualAxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "axes",
                schema: "project",
                table: "portfolio_visual",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_portfolio_visual_axes",
                schema: "project",
                table: "portfolio_visual",
                sql: "axes IS NULL OR visual_type IN ('BAR', 'COLUMN', 'LINE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_portfolio_visual_axes",
                schema: "project",
                table: "portfolio_visual");

            migrationBuilder.DropColumn(
                name: "axes",
                schema: "project",
                table: "portfolio_visual");
        }
    }
}
