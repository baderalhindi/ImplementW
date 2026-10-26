using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-025, 4 of 4: the three <c>identity_access</c> foreign keys that reference other modules' referenceable
    /// tables (ERD D-14) — external entity type and permission classification to <c>master_data_item</c>, and the
    /// per-project grant to <c>project</c> — with their indexes. They could not be declared in 1 of 4, before the
    /// referenced tables existed.
    /// </summary>
    public partial class TASK025_AddIdentityAccessCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_permission_data_classification_item_id",
                schema: "identity_access",
                table: "permission",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_entity_entity_type_item_id",
                schema: "identity_access",
                table: "external_entity",
                column: "entity_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_relationship_project_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_access_relationship_project_project_id",
                schema: "identity_access",
                table: "access_relationship",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_entity_master_data_item_entity_type_item_id",
                schema: "identity_access",
                table: "external_entity",
                column: "entity_type_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_permission_master_data_item_data_classification_item_id",
                schema: "identity_access",
                table: "permission",
                column: "data_classification_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_access_relationship_project_project_id",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.DropForeignKey(
                name: "fk_external_entity_master_data_item_entity_type_item_id",
                schema: "identity_access",
                table: "external_entity");

            migrationBuilder.DropForeignKey(
                name: "fk_permission_master_data_item_data_classification_item_id",
                schema: "identity_access",
                table: "permission");

            migrationBuilder.DropIndex(
                name: "ix_permission_data_classification_item_id",
                schema: "identity_access",
                table: "permission");

            migrationBuilder.DropIndex(
                name: "ix_external_entity_entity_type_item_id",
                schema: "identity_access",
                table: "external_entity");

            migrationBuilder.DropIndex(
                name: "ix_access_relationship_project_id",
                schema: "identity_access",
                table: "access_relationship");
        }
    }
}
