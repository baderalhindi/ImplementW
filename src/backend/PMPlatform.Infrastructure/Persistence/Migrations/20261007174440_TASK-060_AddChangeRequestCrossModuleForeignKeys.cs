using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-060, 2 of 3: the foreign keys from <c>change_request</c> to the tables of other modules that ERD D-14 makes referenceable —
    /// <c>project.project</c> (edge 6), <c>identity_access.user</c>, <c>master_data_config.master_data_item</c> (a requested governance
    /// profile), the pinned <c>master_data_config.configuration_version</c> of an evaluation (ERD D-13), <c>approval.approval_instance</c>
    /// (edge 22), and the commitments an evaluation pins, <c>schedule.project_baseline</c> and <c>financial_kpi.financial_commitment</c> —
    /// and the keys TASK-046 and TASK-052 left for this task: <c>project_baseline.change_authorization_id</c> and
    /// <c>financial_commitment.change_authorization_id</c> to <c>change_request.change_authorization</c> (edges 11, 12), with their indexes.
    /// </summary>
    public partial class TASK060_AddChangeRequestCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_project_baseline_change_authorization_id",
                schema: "schedule",
                table: "project_baseline",
                column: "change_authorization_id");

            migrationBuilder.CreateIndex(
                name: "ix_materiality_evaluation_financial_commitment_id",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "financial_commitment_id");

            migrationBuilder.CreateIndex(
                name: "ix_materiality_evaluation_materiality_configuration_version_id",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "materiality_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_materiality_evaluation_project_baseline_id",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "project_baseline_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_change_authorization_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "change_authorization_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_request_requested_by_user_id",
                schema: "change_request",
                table: "change_request",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_request_requested_governance_profile_item_id",
                schema: "change_request",
                table: "change_request",
                column: "requested_governance_profile_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_authorization_applied_by_user_id",
                schema: "change_request",
                table: "change_authorization",
                column: "applied_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_authorization_approval_instance_id",
                schema: "change_request",
                table: "change_authorization",
                column: "approval_instance_id");

            migrationBuilder.AddForeignKey(
                name: "fk_change_authorization_approval_instance_approval_instance_id",
                schema: "change_request",
                table: "change_authorization",
                column: "approval_instance_id",
                principalSchema: "approval",
                principalTable: "approval_instance",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_change_authorization_user_applied_by_user_id",
                schema: "change_request",
                table: "change_authorization",
                column: "applied_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_change_request_master_data_item_requested_governance_profil",
                schema: "change_request",
                table: "change_request",
                column: "requested_governance_profile_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_change_request_project_project_id",
                schema: "change_request",
                table: "change_request",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_change_request_user_requested_by_user_id",
                schema: "change_request",
                table: "change_request",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_commitment_change_authorization_change_authorizat",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "change_authorization_id",
                principalSchema: "change_request",
                principalTable: "change_authorization",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_materiality_evaluation_financial_commitment_financial_commi",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "financial_commitment_id",
                principalSchema: "financial_kpi",
                principalTable: "financial_commitment",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_materiality_evaluation_materiality_configuration_version",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "materiality_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_materiality_evaluation_project_baseline_project_baseline_id",
                schema: "change_request",
                table: "materiality_evaluation",
                column: "project_baseline_id",
                principalSchema: "schedule",
                principalTable: "project_baseline",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_baseline_change_authorization_change_authorization_",
                schema: "schedule",
                table: "project_baseline",
                column: "change_authorization_id",
                principalSchema: "change_request",
                principalTable: "change_authorization",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_change_authorization_approval_instance_approval_instance_id",
                schema: "change_request",
                table: "change_authorization");

            migrationBuilder.DropForeignKey(
                name: "fk_change_authorization_user_applied_by_user_id",
                schema: "change_request",
                table: "change_authorization");

            migrationBuilder.DropForeignKey(
                name: "fk_change_request_master_data_item_requested_governance_profil",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropForeignKey(
                name: "fk_change_request_project_project_id",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropForeignKey(
                name: "fk_change_request_user_requested_by_user_id",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_commitment_change_authorization_change_authorizat",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropForeignKey(
                name: "fk_materiality_evaluation_financial_commitment_financial_commi",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_materiality_evaluation_materiality_configuration_version",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_materiality_evaluation_project_baseline_project_baseline_id",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropForeignKey(
                name: "fk_project_baseline_change_authorization_change_authorization_",
                schema: "schedule",
                table: "project_baseline");

            migrationBuilder.DropIndex(
                name: "ix_project_baseline_change_authorization_id",
                schema: "schedule",
                table: "project_baseline");

            migrationBuilder.DropIndex(
                name: "ix_materiality_evaluation_financial_commitment_id",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_materiality_evaluation_materiality_configuration_version_id",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_materiality_evaluation_project_baseline_id",
                schema: "change_request",
                table: "materiality_evaluation");

            migrationBuilder.DropIndex(
                name: "ix_financial_commitment_change_authorization_id",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropIndex(
                name: "ix_change_request_requested_by_user_id",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropIndex(
                name: "ix_change_request_requested_governance_profile_item_id",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropIndex(
                name: "ix_change_authorization_applied_by_user_id",
                schema: "change_request",
                table: "change_authorization");

            migrationBuilder.DropIndex(
                name: "ix_change_authorization_approval_instance_id",
                schema: "change_request",
                table: "change_authorization");
        }
    }
}
