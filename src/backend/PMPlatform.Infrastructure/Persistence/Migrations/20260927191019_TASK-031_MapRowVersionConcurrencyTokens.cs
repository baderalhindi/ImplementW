using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-031: maps PostgreSQL's <c>xmin</c> system column as the concurrency token of the four rows FG-03 edits in full
    /// (ERD D-16; api-conventions R-21 ETag and If-Match). <c>xmin</c> exists on every table, so the Npgsql SQL generator
    /// emits no DDL for these operations in either direction: the migration records the model change and alters nothing.
    /// </summary>
    public partial class TASK031_MapRowVersionConcurrencyTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity_access",
                table: "user",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity_access",
                table: "role",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity_access",
                table: "external_entity",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "identity_access",
                table: "department",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity_access",
                table: "role");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity_access",
                table: "external_entity");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "identity_access",
                table: "department");
        }
    }
}
