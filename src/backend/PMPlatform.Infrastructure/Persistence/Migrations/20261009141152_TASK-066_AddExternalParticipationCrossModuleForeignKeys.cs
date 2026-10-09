using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-066, 2 of 3: the foreign keys from <c>external_participation</c> to the tables of other modules that ERD D-14 makes referenceable —
    /// <c>project.project</c> (edge 18), <c>identity_access.external_entity</c> and <c>identity_access.user</c> (E-U1),
    /// <c>master_data_config.master_data_item</c> and <c>master_data_config.configuration_version</c> (E-U2) — with their indexes.
    /// </summary>
    public partial class TASK066_AddExternalParticipationCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_contribution_type_item_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "contribution_type_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_issued_by_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "issued_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_participation_configuration_version",
                schema: "external_participation",
                table: "external_update_request",
                column: "participation_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_external_entity_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "external_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_project_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_external_contribution_external_entity_external_entity_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "external_entity_id",
                principalSchema: "identity_access",
                principalTable: "external_entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_contribution_project_project_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_contribution_user_contributor_user_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "contributor_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_contribution_user_reviewed_by_user_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "reviewed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_configuration_version_participation",
                schema: "external_participation",
                table: "external_update_request",
                column: "participation_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_external_entity_external_entity_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "external_entity_id",
                principalSchema: "identity_access",
                principalTable: "external_entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_master_data_item_contribution_type_",
                schema: "external_participation",
                table: "external_update_request",
                column: "contribution_type_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_project_project_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_user_issued_by_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "issued_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_user_responsible_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "responsible_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_user_reviewer_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "reviewer_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_source_application_user_attempted_by_user_id",
                schema: "external_participation",
                table: "source_application",
                column: "attempted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_source_application_user_revalidated_by_user_id",
                schema: "external_participation",
                table: "source_application",
                column: "revalidated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_external_contribution_external_entity_external_entity_id",
                schema: "external_participation",
                table: "external_contribution");

            migrationBuilder.DropForeignKey(
                name: "fk_external_contribution_project_project_id",
                schema: "external_participation",
                table: "external_contribution");

            migrationBuilder.DropForeignKey(
                name: "fk_external_contribution_user_contributor_user_id",
                schema: "external_participation",
                table: "external_contribution");

            migrationBuilder.DropForeignKey(
                name: "fk_external_contribution_user_reviewed_by_user_id",
                schema: "external_participation",
                table: "external_contribution");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_configuration_version_participation",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_external_entity_external_entity_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_master_data_item_contribution_type_",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_project_project_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_user_issued_by_user_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_user_responsible_user_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_user_reviewer_user_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropForeignKey(
                name: "fk_source_application_user_attempted_by_user_id",
                schema: "external_participation",
                table: "source_application");

            migrationBuilder.DropForeignKey(
                name: "fk_source_application_user_revalidated_by_user_id",
                schema: "external_participation",
                table: "source_application");

            migrationBuilder.DropIndex(
                name: "ix_external_update_request_contribution_type_item_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropIndex(
                name: "ix_external_update_request_issued_by_user_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropIndex(
                name: "ix_external_update_request_participation_configuration_version",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropIndex(
                name: "ix_external_contribution_external_entity_id",
                schema: "external_participation",
                table: "external_contribution");

            migrationBuilder.DropIndex(
                name: "ix_external_contribution_project_id",
                schema: "external_participation",
                table: "external_contribution");
        }
    }
}
