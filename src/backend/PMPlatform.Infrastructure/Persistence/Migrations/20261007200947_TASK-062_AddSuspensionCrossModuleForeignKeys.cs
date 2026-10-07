using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-062, 2 of 4: the foreign keys from <c>suspension</c> to the tables of other modules that ERD D-14 makes referenceable —
    /// <c>project.project</c> from both tables (edge 7) and <c>identity_access.user</c> from a request's requester — with the requester's index.
    /// </summary>
    public partial class TASK062_AddSuspensionCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_suspension_request_requested_by_user_id",
                schema: "suspension",
                table: "suspension_request",
                column: "requested_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_active_suspension_project_project_id",
                schema: "suspension",
                table: "active_suspension",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_suspension_request_project_project_id",
                schema: "suspension",
                table: "suspension_request",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_suspension_request_user_requested_by_user_id",
                schema: "suspension",
                table: "suspension_request",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_active_suspension_project_project_id",
                schema: "suspension",
                table: "active_suspension");

            migrationBuilder.DropForeignKey(
                name: "fk_suspension_request_project_project_id",
                schema: "suspension",
                table: "suspension_request");

            migrationBuilder.DropForeignKey(
                name: "fk_suspension_request_user_requested_by_user_id",
                schema: "suspension",
                table: "suspension_request");

            migrationBuilder.DropIndex(
                name: "ix_suspension_request_requested_by_user_id",
                schema: "suspension",
                table: "suspension_request");
        }
    }
}
