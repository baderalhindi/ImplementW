using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-026, 3 of 3 (<c>docs/architecture/indexing-strategy.md</c> I-01 to I-05): the SCR-025 Project Register's
    /// default sort, <c>updated_at, id</c>, alone and behind the state filter and each data-scope anchor. The department,
    /// project manager and external entity composites replace those foreign keys' single-column indexes.
    /// </summary>
    public partial class TASK026_AddProjectRegisterIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_project_department_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_external_entity_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_project_manager_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.CreateIndex(
                name: "ix_project_department_id_updated_at_id",
                schema: "project",
                table: "project",
                columns: new[] { "department_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_external_entity_id_updated_at_id",
                schema: "project",
                table: "project",
                columns: new[] { "external_entity_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_lifecycle_state_updated_at_id",
                schema: "project",
                table: "project",
                columns: new[] { "lifecycle_state", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_project_manager_user_id_updated_at_id",
                schema: "project",
                table: "project",
                columns: new[] { "project_manager_user_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_updated_at_id",
                schema: "project",
                table: "project",
                columns: new[] { "updated_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_project_department_id_updated_at_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_external_entity_id_updated_at_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_lifecycle_state_updated_at_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_project_manager_user_id_updated_at_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_updated_at_id",
                schema: "project",
                table: "project");

            migrationBuilder.CreateIndex(
                name: "ix_project_department_id",
                schema: "project",
                table: "project",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_external_entity_id",
                schema: "project",
                table: "project",
                column: "external_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_project_manager_user_id",
                schema: "project",
                table: "project",
                column: "project_manager_user_id");
        }
    }
}
