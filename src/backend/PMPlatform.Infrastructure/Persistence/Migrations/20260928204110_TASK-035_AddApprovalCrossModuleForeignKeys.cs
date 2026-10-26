using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-035, 3 of 4: the nine foreign keys from <c>approval</c> to other modules' referenceable tables (ERD D-14) —
    /// the pinned APPROVAL_AUTHORITY version, the scope anchors, the requester, the task's role and users, and both
    /// sides of a delegation — with the indexes of the five that no register index already leads with.
    /// </summary>
    public partial class TASK035_AddApprovalCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_approval_task_acting_user_id",
                schema: "approval",
                table: "approval_task",
                column: "acting_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_authority_configuration_version_id",
                schema: "approval",
                table: "approval_instance",
                column: "authority_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_scope_department_id",
                schema: "approval",
                table: "approval_instance",
                column: "scope_department_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_scope_project_id",
                schema: "approval",
                table: "approval_instance",
                column: "scope_project_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_delegation_delegator_user_id",
                schema: "approval",
                table: "approval_delegation",
                column: "delegator_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_approval_delegation_user_delegate_user_id",
                schema: "approval",
                table: "approval_delegation",
                column: "delegate_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_delegation_user_delegator_user_id",
                schema: "approval",
                table: "approval_delegation",
                column: "delegator_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_instance_authority_configuration_version",
                schema: "approval",
                table: "approval_instance",
                column: "authority_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_instance_department_scope_department_id",
                schema: "approval",
                table: "approval_instance",
                column: "scope_department_id",
                principalSchema: "identity_access",
                principalTable: "department",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_instance_project_scope_project_id",
                schema: "approval",
                table: "approval_instance",
                column: "scope_project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_instance_user_requested_by_user_id",
                schema: "approval",
                table: "approval_instance",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_task_role_assigned_role_id",
                schema: "approval",
                table: "approval_task",
                column: "assigned_role_id",
                principalSchema: "identity_access",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_task_user_acting_user_id",
                schema: "approval",
                table: "approval_task",
                column: "acting_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_approval_task_user_assigned_user_id",
                schema: "approval",
                table: "approval_task",
                column: "assigned_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_approval_delegation_user_delegate_user_id",
                schema: "approval",
                table: "approval_delegation");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_delegation_user_delegator_user_id",
                schema: "approval",
                table: "approval_delegation");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_instance_authority_configuration_version",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_instance_department_scope_department_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_instance_project_scope_project_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_instance_user_requested_by_user_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_task_role_assigned_role_id",
                schema: "approval",
                table: "approval_task");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_task_user_acting_user_id",
                schema: "approval",
                table: "approval_task");

            migrationBuilder.DropForeignKey(
                name: "fk_approval_task_user_assigned_user_id",
                schema: "approval",
                table: "approval_task");

            migrationBuilder.DropIndex(
                name: "ix_approval_task_acting_user_id",
                schema: "approval",
                table: "approval_task");

            migrationBuilder.DropIndex(
                name: "ix_approval_instance_authority_configuration_version_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropIndex(
                name: "ix_approval_instance_scope_department_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropIndex(
                name: "ix_approval_instance_scope_project_id",
                schema: "approval",
                table: "approval_instance");

            migrationBuilder.DropIndex(
                name: "ix_approval_delegation_delegator_user_id",
                schema: "approval",
                table: "approval_delegation");
        }
    }
}
