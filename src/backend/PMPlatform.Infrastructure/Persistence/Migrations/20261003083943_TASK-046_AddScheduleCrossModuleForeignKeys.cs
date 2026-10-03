using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-046, 2 of 4: the foreign keys from <c>schedule</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>project.project</c>, <c>project.project_intake</c> (ADR-014's Declared Baseline),
    /// <c>master_data_config.master_data_item</c> (the working calendar) and <c>master_data_config.configuration_version</c>
    /// (the pinned health rule) — with their indexes. <c>change_authorization_id</c>'s key to
    /// <c>change_request.change_authorization</c> is TASK-060's, whose migration creates that table.
    /// </summary>
    public partial class TASK046_AddScheduleCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_schedule_health_status_health_rule_configuration_version_id",
                schema: "schedule",
                table: "schedule_health_status",
                column: "health_rule_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_schedule_calendar_item_id",
                schema: "schedule",
                table: "project_schedule",
                column: "calendar_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_baseline_project_intake_project_intake_id",
                schema: "schedule",
                table: "project_baseline",
                column: "project_intake_id",
                principalSchema: "project",
                principalTable: "project_intake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_baseline_project_project_id",
                schema: "schedule",
                table: "project_baseline",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_schedule_master_data_item_calendar_item_id",
                schema: "schedule",
                table: "project_schedule",
                column: "calendar_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_schedule_project_project_id",
                schema: "schedule",
                table: "project_schedule",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_schedule_health_status_configuration_version_health_rule_co",
                schema: "schedule",
                table: "schedule_health_status",
                column: "health_rule_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_schedule_health_status_project_project_id",
                schema: "schedule",
                table: "schedule_health_status",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_baseline_project_intake_project_intake_id",
                schema: "schedule",
                table: "project_baseline");

            migrationBuilder.DropForeignKey(
                name: "fk_project_baseline_project_project_id",
                schema: "schedule",
                table: "project_baseline");

            migrationBuilder.DropForeignKey(
                name: "fk_project_schedule_master_data_item_calendar_item_id",
                schema: "schedule",
                table: "project_schedule");

            migrationBuilder.DropForeignKey(
                name: "fk_project_schedule_project_project_id",
                schema: "schedule",
                table: "project_schedule");

            migrationBuilder.DropForeignKey(
                name: "fk_schedule_health_status_configuration_version_health_rule_co",
                schema: "schedule",
                table: "schedule_health_status");

            migrationBuilder.DropForeignKey(
                name: "fk_schedule_health_status_project_project_id",
                schema: "schedule",
                table: "schedule_health_status");

            migrationBuilder.DropIndex(
                name: "ix_schedule_health_status_health_rule_configuration_version_id",
                schema: "schedule",
                table: "schedule_health_status");

            migrationBuilder.DropIndex(
                name: "ix_project_schedule_calendar_item_id",
                schema: "schedule",
                table: "project_schedule");
        }
    }
}
