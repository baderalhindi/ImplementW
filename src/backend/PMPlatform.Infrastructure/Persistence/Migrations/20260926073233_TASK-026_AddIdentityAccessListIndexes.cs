using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-026, 1 of 3 (<c>docs/architecture/indexing-strategy.md</c> I-06 to I-08): a user's and a project's grants by
    /// status, which replace the single-column <c>user_id</c> and <c>project_id</c> indexes they lead with, and the
    /// ADM-002 user list by status and name.
    /// </summary>
    public partial class TASK026_AddIdentityAccessListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_access_relationship_project_id",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.DropIndex(
                name: "ix_access_relationship_user_id",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.CreateIndex(
                name: "ix_user_status_display_name_id",
                schema: "identity_access",
                table: "user",
                columns: new[] { "status", "display_name", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_project_id_status",
                schema: "identity_access",
                table: "access_relationship",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_user_id_status",
                schema: "identity_access",
                table: "access_relationship",
                columns: new[] { "user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_status_display_name_id",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_access_relationship_project_id_status",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.DropIndex(
                name: "ix_access_relationship_user_id_status",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_project_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_user_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "user_id");
        }
    }
}
