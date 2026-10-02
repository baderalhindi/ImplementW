using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-044, 2 of 3: the foreign keys from <c>progress</c> to the tables of other modules that ERD D-14 makes
    /// referenceable — <c>project.project</c>, <c>project.project_intake</c> (the ADR-014 opening position),
    /// <c>identity_access.user</c> (submitter, reviewer, publisher) and <c>master_data_config.configuration_version</c> (the
    /// pinned health rule) — with their indexes. <c>baseline_id</c>'s key to <c>schedule.project_baseline</c> is TASK-046's,
    /// whose migration creates that table.
    /// </summary>
    public partial class TASK044_AddProgressCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_published_progress_snapshot_health_rule_configuration_versi",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "health_rule_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_progress_snapshot_published_by_user_id",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_health_status_health_rule_configuration_version_id",
                schema: "progress",
                table: "project_health_status",
                column: "health_rule_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_project_intake_id",
                schema: "progress",
                table: "progress_submission",
                column: "project_intake_id");

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_reviewed_by_user_id",
                schema: "progress",
                table: "progress_submission",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_submitted_by_user_id",
                schema: "progress",
                table: "progress_submission",
                column: "submitted_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_project_intake_project_intake_id",
                schema: "progress",
                table: "progress_submission",
                column: "project_intake_id",
                principalSchema: "project",
                principalTable: "project_intake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_project_project_id",
                schema: "progress",
                table: "progress_submission",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_user_reviewed_by_user_id",
                schema: "progress",
                table: "progress_submission",
                column: "reviewed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_user_submitted_by_user_id",
                schema: "progress",
                table: "progress_submission",
                column: "submitted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_health_status_configuration_version_health_rule_con",
                schema: "progress",
                table: "project_health_status",
                column: "health_rule_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_health_status_project_project_id",
                schema: "progress",
                table: "project_health_status",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_progress_snapshot_configuration_version_health_ru",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "health_rule_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_progress_snapshot_project_project_id",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_progress_snapshot_user_published_by_user_id",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_reporting_cycle_project_project_id",
                schema: "progress",
                table: "reporting_cycle",
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
                name: "fk_progress_submission_project_intake_project_intake_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropForeignKey(
                name: "fk_progress_submission_project_project_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropForeignKey(
                name: "fk_progress_submission_user_reviewed_by_user_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropForeignKey(
                name: "fk_progress_submission_user_submitted_by_user_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropForeignKey(
                name: "fk_project_health_status_configuration_version_health_rule_con",
                schema: "progress",
                table: "project_health_status");

            migrationBuilder.DropForeignKey(
                name: "fk_project_health_status_project_project_id",
                schema: "progress",
                table: "project_health_status");

            migrationBuilder.DropForeignKey(
                name: "fk_published_progress_snapshot_configuration_version_health_ru",
                schema: "progress",
                table: "published_progress_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_progress_snapshot_project_project_id",
                schema: "progress",
                table: "published_progress_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_progress_snapshot_user_published_by_user_id",
                schema: "progress",
                table: "published_progress_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_reporting_cycle_project_project_id",
                schema: "progress",
                table: "reporting_cycle");

            migrationBuilder.DropIndex(
                name: "ix_published_progress_snapshot_health_rule_configuration_versi",
                schema: "progress",
                table: "published_progress_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_published_progress_snapshot_published_by_user_id",
                schema: "progress",
                table: "published_progress_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_project_health_status_health_rule_configuration_version_id",
                schema: "progress",
                table: "project_health_status");

            migrationBuilder.DropIndex(
                name: "ix_progress_submission_project_intake_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropIndex(
                name: "ix_progress_submission_reviewed_by_user_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropIndex(
                name: "ix_progress_submission_submitted_by_user_id",
                schema: "progress",
                table: "progress_submission");
        }
    }
}
