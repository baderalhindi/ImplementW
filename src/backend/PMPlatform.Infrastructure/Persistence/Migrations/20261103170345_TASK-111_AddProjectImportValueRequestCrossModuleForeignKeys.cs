using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK111_AddProjectImportValueRequestCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_project_import_value_request_resolved_by_user_id",
                schema: "project",
                table: "project_import_value_request",
                column: "resolved_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_import_value_request_user_resolved_by_user_id",
                schema: "project",
                table: "project_import_value_request",
                column: "resolved_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_import_value_request_user_resolved_by_user_id",
                schema: "project",
                table: "project_import_value_request");

            migrationBuilder.DropIndex(
                name: "ix_project_import_value_request_resolved_by_user_id",
                schema: "project",
                table: "project_import_value_request");
        }
    }
}
