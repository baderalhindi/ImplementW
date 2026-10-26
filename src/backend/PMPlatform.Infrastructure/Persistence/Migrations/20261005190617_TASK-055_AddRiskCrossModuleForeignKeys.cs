using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-055, 2 of 3: the foreign keys from <c>risk</c> to the tables of other modules that ERD D-14 makes referenceable —
    /// <c>project.project</c> (edge 3), <c>identity_access.user</c> (owners, assessor, acceptor, closer),
    /// <c>master_data_config.master_data_item</c> (category, impact dimension), and the pinned
    /// <c>master_data_config.configuration_version</c> and <c>master_data_config.risk_rating_definition</c> of an assessment
    /// (ERD D-13, §7 row 12) — with their indexes.
    /// </summary>
    public partial class TASK055_AddRiskCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_risk_treatment_action_owner_user_id",
                schema: "risk",
                table: "risk_treatment_action",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_version_assessed_by_user_id",
                schema: "risk",
                table: "risk_assessment_version",
                column: "assessed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_version_matrix_configuration_version_id",
                schema: "risk",
                table: "risk_assessment_version",
                column: "matrix_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_version_risk_rating_definition_id",
                schema: "risk",
                table: "risk_assessment_version",
                column: "risk_rating_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_impact_impact_dimension_item_id",
                schema: "risk",
                table: "risk_assessment_impact",
                column: "impact_dimension_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_acceptance_accepted_by_user_id",
                schema: "risk",
                table: "risk_acceptance",
                column: "accepted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_closed_by_user_id",
                schema: "risk",
                table: "risk",
                column: "closed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_risk_category_item_id",
                schema: "risk",
                table: "risk",
                column: "risk_category_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_risk_master_data_item_risk_category_item_id",
                schema: "risk",
                table: "risk",
                column: "risk_category_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_project_project_id",
                schema: "risk",
                table: "risk",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_user_closed_by_user_id",
                schema: "risk",
                table: "risk",
                column: "closed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_user_owner_user_id",
                schema: "risk",
                table: "risk",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_acceptance_user_accepted_by_user_id",
                schema: "risk",
                table: "risk_acceptance",
                column: "accepted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_assessment_impact_master_data_item_impact_dimension_it",
                schema: "risk",
                table: "risk_assessment_impact",
                column: "impact_dimension_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_assessment_version_configuration_version_matrix_config",
                schema: "risk",
                table: "risk_assessment_version",
                column: "matrix_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_assessment_version_risk_rating_definition_risk_rating_",
                schema: "risk",
                table: "risk_assessment_version",
                column: "risk_rating_definition_id",
                principalSchema: "master_data_config",
                principalTable: "risk_rating_definition",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_assessment_version_user_assessed_by_user_id",
                schema: "risk",
                table: "risk_assessment_version",
                column: "assessed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_treatment_action_user_owner_user_id",
                schema: "risk",
                table: "risk_treatment_action",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_risk_master_data_item_risk_category_item_id",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_project_project_id",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_user_closed_by_user_id",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_user_owner_user_id",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_acceptance_user_accepted_by_user_id",
                schema: "risk",
                table: "risk_acceptance");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_assessment_impact_master_data_item_impact_dimension_it",
                schema: "risk",
                table: "risk_assessment_impact");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_assessment_version_configuration_version_matrix_config",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_assessment_version_risk_rating_definition_risk_rating_",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_assessment_version_user_assessed_by_user_id",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_treatment_action_user_owner_user_id",
                schema: "risk",
                table: "risk_treatment_action");

            migrationBuilder.DropIndex(
                name: "ix_risk_treatment_action_owner_user_id",
                schema: "risk",
                table: "risk_treatment_action");

            migrationBuilder.DropIndex(
                name: "ix_risk_assessment_version_assessed_by_user_id",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropIndex(
                name: "ix_risk_assessment_version_matrix_configuration_version_id",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropIndex(
                name: "ix_risk_assessment_version_risk_rating_definition_id",
                schema: "risk",
                table: "risk_assessment_version");

            migrationBuilder.DropIndex(
                name: "ix_risk_assessment_impact_impact_dimension_item_id",
                schema: "risk",
                table: "risk_assessment_impact");

            migrationBuilder.DropIndex(
                name: "ix_risk_acceptance_accepted_by_user_id",
                schema: "risk",
                table: "risk_acceptance");

            migrationBuilder.DropIndex(
                name: "ix_risk_closed_by_user_id",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropIndex(
                name: "ix_risk_risk_category_item_id",
                schema: "risk",
                table: "risk");
        }
    }
}
