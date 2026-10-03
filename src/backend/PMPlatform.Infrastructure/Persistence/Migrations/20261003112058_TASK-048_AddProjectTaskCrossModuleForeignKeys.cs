using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-048, 2 of 3: the foreign keys from <c>project_task</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>project.project</c>, <c>schedule.schedule_activity</c> (edge 9), <c>identity_access.user</c> (the
    /// task owner) and <c>master_data_config.master_data_item</c> (the priority) — with their indexes.
    /// </summary>
    public partial class TASK048_AddProjectTaskCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_project_task_priority_item_id",
                schema: "project_task",
                table: "project_task",
                column: "priority_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_task_schedule_activity_id",
                schema: "project_task",
                table: "project_task",
                column: "schedule_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_execution_progress_project_id",
                schema: "project_task",
                table: "activity_execution_progress",
                column: "project_id");

            migrationBuilder.AddForeignKey(
                name: "fk_activity_execution_progress_project_project_id",
                schema: "project_task",
                table: "activity_execution_progress",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_activity_execution_progress_schedule_activity_schedule_acti",
                schema: "project_task",
                table: "activity_execution_progress",
                column: "schedule_activity_id",
                principalSchema: "schedule",
                principalTable: "schedule_activity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_task_master_data_item_priority_item_id",
                schema: "project_task",
                table: "project_task",
                column: "priority_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_task_project_project_id",
                schema: "project_task",
                table: "project_task",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_task_schedule_activity_schedule_activity_id",
                schema: "project_task",
                table: "project_task",
                column: "schedule_activity_id",
                principalSchema: "schedule",
                principalTable: "schedule_activity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_task_user_assignee_user_id",
                schema: "project_task",
                table: "project_task",
                column: "assignee_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_activity_execution_progress_project_project_id",
                schema: "project_task",
                table: "activity_execution_progress");

            migrationBuilder.DropForeignKey(
                name: "fk_activity_execution_progress_schedule_activity_schedule_acti",
                schema: "project_task",
                table: "activity_execution_progress");

            migrationBuilder.DropForeignKey(
                name: "fk_project_task_master_data_item_priority_item_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropForeignKey(
                name: "fk_project_task_project_project_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropForeignKey(
                name: "fk_project_task_schedule_activity_schedule_activity_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropForeignKey(
                name: "fk_project_task_user_assignee_user_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropIndex(
                name: "ix_project_task_priority_item_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropIndex(
                name: "ix_project_task_schedule_activity_id",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropIndex(
                name: "ix_activity_execution_progress_project_id",
                schema: "project_task",
                table: "activity_execution_progress");
        }
    }
}
