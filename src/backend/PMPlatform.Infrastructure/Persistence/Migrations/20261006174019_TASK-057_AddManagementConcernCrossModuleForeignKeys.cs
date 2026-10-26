using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-057, 2 of 3: the foreign keys from <c>management_concern</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>project.project</c> (edge 4), <c>risk.risk</c> (edge 15's one foreign key, <c>originating_risk_id</c>),
    /// <c>identity_access.user</c> and <c>identity_access.role</c>, <c>master_data_config.master_data_item</c> (category, priority,
    /// severity, impact dimension) and the pinned <c>master_data_config.configuration_version</c> of a severity (ERD D-13) — with their
    /// indexes.
    /// </summary>
    public partial class TASK057_AddManagementConcernCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_management_concern_category_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "category_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_priority_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "priority_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_raised_by_user_id",
                schema: "management_concern",
                table: "management_concern",
                column: "raised_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_severity_configuration_version_id",
                schema: "management_concern",
                table: "management_concern",
                column: "severity_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_severity_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "severity_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_concern_impact_impact_dimension_item_id",
                schema: "management_concern",
                table: "concern_impact",
                column: "impact_dimension_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_resolved_by_user_id",
                schema: "management_concern",
                table: "concern_escalation",
                column: "resolved_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_concern_escalation_role_escalated_to_role_id",
                schema: "management_concern",
                table: "concern_escalation",
                column: "escalated_to_role_id",
                principalSchema: "identity_access",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_concern_escalation_user_escalated_by_user_id",
                schema: "management_concern",
                table: "concern_escalation",
                column: "escalated_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_concern_escalation_user_resolved_by_user_id",
                schema: "management_concern",
                table: "concern_escalation",
                column: "resolved_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_concern_impact_master_data_item_impact_dimension_item_id",
                schema: "management_concern",
                table: "concern_impact",
                column: "impact_dimension_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_severity_configuration_version",
                schema: "management_concern",
                table: "management_concern",
                column: "severity_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_master_data_item_category_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "category_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_master_data_item_priority_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "priority_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_master_data_item_severity_item_id",
                schema: "management_concern",
                table: "management_concern",
                column: "severity_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_project_project_id",
                schema: "management_concern",
                table: "management_concern",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_risk_originating_risk_id",
                schema: "management_concern",
                table: "management_concern",
                column: "originating_risk_id",
                principalSchema: "risk",
                principalTable: "risk",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_user_assignee_user_id",
                schema: "management_concern",
                table: "management_concern",
                column: "assignee_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_management_concern_user_raised_by_user_id",
                schema: "management_concern",
                table: "management_concern",
                column: "raised_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_concern_escalation_role_escalated_to_role_id",
                schema: "management_concern",
                table: "concern_escalation");

            migrationBuilder.DropForeignKey(
                name: "fk_concern_escalation_user_escalated_by_user_id",
                schema: "management_concern",
                table: "concern_escalation");

            migrationBuilder.DropForeignKey(
                name: "fk_concern_escalation_user_resolved_by_user_id",
                schema: "management_concern",
                table: "concern_escalation");

            migrationBuilder.DropForeignKey(
                name: "fk_concern_impact_master_data_item_impact_dimension_item_id",
                schema: "management_concern",
                table: "concern_impact");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_severity_configuration_version",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_master_data_item_category_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_master_data_item_priority_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_master_data_item_severity_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_project_project_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_risk_originating_risk_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_user_assignee_user_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropForeignKey(
                name: "fk_management_concern_user_raised_by_user_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_management_concern_category_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_management_concern_priority_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_management_concern_raised_by_user_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_management_concern_severity_configuration_version_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_management_concern_severity_item_id",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropIndex(
                name: "ix_concern_impact_impact_dimension_item_id",
                schema: "management_concern",
                table: "concern_impact");

            migrationBuilder.DropIndex(
                name: "ix_concern_escalation_resolved_by_user_id",
                schema: "management_concern",
                table: "concern_escalation");
        }
    }
}
