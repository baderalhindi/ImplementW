using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-052, 2 of 3: the keys that leave the <c>financial_kpi</c> schema (README R-7) — to the project and its intake, users,
    /// master data items (ETIMAD_COST_CATEGORY, MEASUREMENT_FREQUENCY), the KPI catalogue, WF-02's reporting cycles (edge 13) and
    /// the WORKFLOW_POLICY version a snapshot pins — with their indexes. <c>change_authorization_id</c> has none until TASK-060
    /// creates its table.
    /// </summary>
    public partial class TASK052_AddFinancialKpiCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_entered_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "entered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_published_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_threshold_configuration_versio",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "threshold_configuration_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_measurement_published_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement",
                column: "published_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_measurement_recorded_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement",
                column: "recorded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_assignment_kpi_definition_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "kpi_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_assignment_measurement_frequency_item_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "measurement_frequency_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_assignment_owner_user_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_line_etimad_category_item_id",
                schema: "financial_kpi",
                table: "financial_progress_update_line",
                column: "etimad_category_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "entered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_project_intake_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "project_intake_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_reviewed_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_submitted_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "submitted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_line_etimad_category_item_id",
                schema: "financial_kpi",
                table: "financial_commitment_line",
                column: "etimad_category_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "entered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_project_intake_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "project_intake_id");

            migrationBuilder.AddForeignKey(
                name: "fk_financial_commitment_project_intake_project_intake_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "project_intake_id",
                principalSchema: "project",
                principalTable: "project_intake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_commitment_project_project_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_commitment_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "entered_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_commitment_line_master_data_item_etimad_category_",
                schema: "financial_kpi",
                table: "financial_commitment_line",
                column: "etimad_category_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_project_intake_project_intake_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "project_intake_id",
                principalSchema: "project",
                principalTable: "project_intake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_project_project_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_reporting_cycle_reporting_cycle_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "reporting_cycle_id",
                principalSchema: "progress",
                principalTable: "reporting_cycle",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "entered_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_user_reviewed_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "reviewed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_user_submitted_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "submitted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_progress_update_line_master_data_item_etimad_cate",
                schema: "financial_kpi",
                table: "financial_progress_update_line",
                column: "etimad_category_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_financial_source_mode_project_project_id",
                schema: "financial_kpi",
                table: "financial_source_mode",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_assignment_kpi_definition_kpi_definition_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "kpi_definition_id",
                principalSchema: "master_data_config",
                principalTable: "kpi_definition",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_assignment_master_data_item_measurement_frequency_item_",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "measurement_frequency_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_assignment_project_project_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_assignment_user_owner_user_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_measurement_user_published_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_kpi_measurement_user_recorded_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement",
                column: "recorded_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_financial_snapshot_configuration_version_threshol",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "threshold_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_financial_snapshot_project_project_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_financial_snapshot_reporting_cycle_reporting_cycl",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "reporting_cycle_id",
                principalSchema: "progress",
                principalTable: "reporting_cycle",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_financial_snapshot_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "entered_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_published_financial_snapshot_user_published_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "published_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_financial_commitment_project_intake_project_intake_id",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_commitment_project_project_id",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_commitment_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_commitment_line_master_data_item_etimad_category_",
                schema: "financial_kpi",
                table: "financial_commitment_line");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_project_intake_project_intake_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_project_project_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_reporting_cycle_reporting_cycle_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_user_reviewed_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_user_submitted_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_progress_update_line_master_data_item_etimad_cate",
                schema: "financial_kpi",
                table: "financial_progress_update_line");

            migrationBuilder.DropForeignKey(
                name: "fk_financial_source_mode_project_project_id",
                schema: "financial_kpi",
                table: "financial_source_mode");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_assignment_kpi_definition_kpi_definition_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_assignment_master_data_item_measurement_frequency_item_",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_assignment_project_project_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_assignment_user_owner_user_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_measurement_user_published_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_kpi_measurement_user_recorded_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement");

            migrationBuilder.DropForeignKey(
                name: "fk_published_financial_snapshot_configuration_version_threshol",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_financial_snapshot_project_project_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_financial_snapshot_reporting_cycle_reporting_cycl",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_financial_snapshot_user_entered_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropForeignKey(
                name: "fk_published_financial_snapshot_user_published_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_published_financial_snapshot_entered_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_published_financial_snapshot_published_by_user_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_published_financial_snapshot_threshold_configuration_versio",
                schema: "financial_kpi",
                table: "published_financial_snapshot");

            migrationBuilder.DropIndex(
                name: "ix_kpi_measurement_published_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement");

            migrationBuilder.DropIndex(
                name: "ix_kpi_measurement_recorded_by_user_id",
                schema: "financial_kpi",
                table: "kpi_measurement");

            migrationBuilder.DropIndex(
                name: "ix_kpi_assignment_kpi_definition_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropIndex(
                name: "ix_kpi_assignment_measurement_frequency_item_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropIndex(
                name: "ix_kpi_assignment_owner_user_id",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropIndex(
                name: "ix_financial_progress_update_line_etimad_category_item_id",
                schema: "financial_kpi",
                table: "financial_progress_update_line");

            migrationBuilder.DropIndex(
                name: "ix_financial_progress_update_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropIndex(
                name: "ix_financial_progress_update_project_intake_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropIndex(
                name: "ix_financial_progress_update_reviewed_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropIndex(
                name: "ix_financial_progress_update_submitted_by_user_id",
                schema: "financial_kpi",
                table: "financial_progress_update");

            migrationBuilder.DropIndex(
                name: "ix_financial_commitment_line_etimad_category_item_id",
                schema: "financial_kpi",
                table: "financial_commitment_line");

            migrationBuilder.DropIndex(
                name: "ix_financial_commitment_entered_by_user_id",
                schema: "financial_kpi",
                table: "financial_commitment");

            migrationBuilder.DropIndex(
                name: "ix_financial_commitment_project_intake_id",
                schema: "financial_kpi",
                table: "financial_commitment");
        }
    }
}
