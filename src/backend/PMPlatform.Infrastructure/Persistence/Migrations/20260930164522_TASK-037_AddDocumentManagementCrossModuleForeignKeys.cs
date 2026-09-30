using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-037, 2 of 3: the foreign keys from <c>document_management</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>project.project</c>, <c>identity_access.user</c> and <c>master_data_config.master_data_item</c> —
    /// with their indexes.
    /// </summary>
    public partial class TASK037_AddDocumentManagementCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_evidence_reference_designated_by_user_id",
                schema: "document_management",
                table: "evidence_reference",
                column: "designated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_reference_evidence_type_item_id",
                schema: "document_management",
                table: "evidence_reference",
                column: "evidence_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_version_uploaded_by_user_id",
                schema: "document_management",
                table: "document_version",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_data_classification_item_id",
                schema: "document_management",
                table: "document",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_document_type_item_id",
                schema: "document_management",
                table: "document",
                column: "document_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_owner_user_id",
                schema: "document_management",
                table: "document",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_link_linked_by_user_id",
                schema: "document_management",
                table: "business_link",
                column: "linked_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_business_link_unlinked_by_user_id",
                schema: "document_management",
                table: "business_link",
                column: "unlinked_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_business_link_user_linked_by_user_id",
                schema: "document_management",
                table: "business_link",
                column: "linked_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_business_link_user_unlinked_by_user_id",
                schema: "document_management",
                table: "business_link",
                column: "unlinked_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_master_data_item_data_classification_item_id",
                schema: "document_management",
                table: "document",
                column: "data_classification_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_master_data_item_document_type_item_id",
                schema: "document_management",
                table: "document",
                column: "document_type_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_project_project_id",
                schema: "document_management",
                table: "document",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_user_owner_user_id",
                schema: "document_management",
                table: "document",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_version_user_uploaded_by_user_id",
                schema: "document_management",
                table: "document_version",
                column: "uploaded_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_evidence_reference_master_data_item_evidence_type_item_id",
                schema: "document_management",
                table: "evidence_reference",
                column: "evidence_type_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_evidence_reference_user_designated_by_user_id",
                schema: "document_management",
                table: "evidence_reference",
                column: "designated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_business_link_user_linked_by_user_id",
                schema: "document_management",
                table: "business_link");

            migrationBuilder.DropForeignKey(
                name: "fk_business_link_user_unlinked_by_user_id",
                schema: "document_management",
                table: "business_link");

            migrationBuilder.DropForeignKey(
                name: "fk_document_master_data_item_data_classification_item_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropForeignKey(
                name: "fk_document_master_data_item_document_type_item_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropForeignKey(
                name: "fk_document_project_project_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropForeignKey(
                name: "fk_document_user_owner_user_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropForeignKey(
                name: "fk_document_version_user_uploaded_by_user_id",
                schema: "document_management",
                table: "document_version");

            migrationBuilder.DropForeignKey(
                name: "fk_evidence_reference_master_data_item_evidence_type_item_id",
                schema: "document_management",
                table: "evidence_reference");

            migrationBuilder.DropForeignKey(
                name: "fk_evidence_reference_user_designated_by_user_id",
                schema: "document_management",
                table: "evidence_reference");

            migrationBuilder.DropIndex(
                name: "ix_evidence_reference_designated_by_user_id",
                schema: "document_management",
                table: "evidence_reference");

            migrationBuilder.DropIndex(
                name: "ix_evidence_reference_evidence_type_item_id",
                schema: "document_management",
                table: "evidence_reference");

            migrationBuilder.DropIndex(
                name: "ix_document_version_uploaded_by_user_id",
                schema: "document_management",
                table: "document_version");

            migrationBuilder.DropIndex(
                name: "ix_document_data_classification_item_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropIndex(
                name: "ix_document_document_type_item_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropIndex(
                name: "ix_document_owner_user_id",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropIndex(
                name: "ix_business_link_linked_by_user_id",
                schema: "document_management",
                table: "business_link");

            migrationBuilder.DropIndex(
                name: "ix_business_link_unlinked_by_user_id",
                schema: "document_management",
                table: "business_link");
        }
    }
}
