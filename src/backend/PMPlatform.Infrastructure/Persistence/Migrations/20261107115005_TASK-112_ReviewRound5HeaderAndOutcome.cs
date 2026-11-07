using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_ReviewRound5HeaderAndOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "show_create_in_header",
                schema: "identity_access",
                table: "role",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_canceled",
                schema: "project",
                table: "project",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_canceled",
                schema: "project",
                table: "project",
                sql: "NOT is_canceled OR lifecycle_state = 'CLOSED'");

            // R02 and R03 rarely create projects: Create leaves their top bar, and stays in the menu (review round 5, item 14).
            migrationBuilder.Sql("UPDATE identity_access.role SET show_create_in_header = false WHERE code IN ('R02', 'R03');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_project_canceled",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "show_create_in_header",
                schema: "identity_access",
                table: "role");

            migrationBuilder.DropColumn(
                name: "is_canceled",
                schema: "project",
                table: "project");
        }
    }
}
