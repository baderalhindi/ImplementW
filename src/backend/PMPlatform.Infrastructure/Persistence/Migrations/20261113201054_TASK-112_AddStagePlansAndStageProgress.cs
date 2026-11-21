using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddStagePlansAndStageProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Widened, then re-added below: a stage-weighted source, planned progress from the stage plan, a STAGE_PLAN_CHANGE authorisation.
            migrationBuilder.Sql("""
                ALTER TABLE progress.progress_submission DROP CONSTRAINT ck_progress_submission_actual_source; -- EXPAND-THEN-CONTRACT-REVIEWED: widened to STAGE_WEIGHTED, re-added below
                ALTER TABLE progress.progress_submission DROP CONSTRAINT ck_progress_submission_planned_baseline; -- EXPAND-THEN-CONTRACT-REVIEWED: widened to a stage plan's planned figure, re-added below
                ALTER TABLE change_request.change_authorization DROP CONSTRAINT ck_change_authorization_authorization_scope; -- EXPAND-THEN-CONTRACT-REVIEWED: widened to STAGE_PLAN_CHANGE, re-added below
                """);

            migrationBuilder.AddColumn<string>(
                name: "current_stage_code",
                schema: "project",
                table: "project",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "current_stage_percent",
                schema: "project",
                table: "project",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "stage_plan_revision_no",
                schema: "project",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "stage_configuration_version_id",
                schema: "progress",
                table: "progress_submission",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "progress_stage_entry",
                schema: "progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    weight_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    applies = table.Column<bool>(type: "boolean", nullable: false),
                    planned_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    percent_complete = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    is_from_schedule = table.Column<bool>(type: "boolean", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_finish_date = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    decrease_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    decrease_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_stage_entry", x => x.id);
                    table.CheckConstraint("ck_progress_stage_entry_actual_dates", "actual_start_date IS NULL OR actual_finish_date IS NULL OR actual_start_date <= actual_finish_date");
                    table.CheckConstraint("ck_progress_stage_entry_decrease_reason_lang", "\"decrease_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_progress_stage_entry_decrease_reason_pair", "(\"decrease_reason\" IS NULL) = (\"decrease_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_progress_stage_entry_percent", "weight_percent BETWEEN 0 AND 100 AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100) AND (percent_complete IS NULL OR percent_complete BETWEEN 0 AND 100)");
                    table.ForeignKey(
                        name: "fk_progress_stage_entry_progress_submission_progress_submissio",
                        column: x => x.progress_submission_id,
                        principalSchema: "progress",
                        principalTable: "progress_submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_stage_plan",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    applies = table.Column<bool>(type: "boolean", nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    planned_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    starting_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_stage_plan", x => x.id);
                    table.CheckConstraint("ck_project_stage_plan_dates", "planned_start_date IS NULL OR planned_end_date IS NULL OR planned_start_date <= planned_end_date");
                    table.CheckConstraint("ck_project_stage_plan_starting_percent", "starting_percent IS NULL OR starting_percent BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "fk_project_stage_plan_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "project",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_current_stage_percent",
                schema: "project",
                table: "project",
                sql: "current_stage_percent IS NULL OR (current_stage_code IS NOT NULL AND current_stage_percent BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_stage_plan_revision_no",
                schema: "project",
                table: "project",
                sql: "stage_plan_revision_no >= 0");

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_stage_configuration_version_id",
                schema: "progress",
                table: "progress_submission",
                column: "stage_configuration_version_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_progress_submission_actual_source",
                schema: "progress",
                table: "progress_submission",
                sql: "\"actual_source\" IN ('WORK_BREAKDOWN', 'DECLARED', 'NOT_AVAILABLE', 'STAGE_WEIGHTED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_progress_submission_planned_baseline",
                schema: "progress",
                table: "progress_submission",
                sql: "planned_percent IS NULL OR baseline_id IS NOT NULL OR stage_configuration_version_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_authorization_authorization_scope",
                schema: "change_request",
                table: "change_authorization",
                sql: "\"authorization_scope\" IN ('REBASELINE', 'COMMITMENT_CHANGE', 'PROFILE_CHANGE', 'SCOPE_CHANGE', 'STAGE_PLAN_CHANGE')");

            migrationBuilder.CreateIndex(
                name: "ix_progress_stage_entry_progress_submission_id_stage_code",
                schema: "progress",
                table: "progress_stage_entry",
                columns: new[] { "progress_submission_id", "stage_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_stage_plan_project_id_stage_code",
                schema: "project",
                table: "project_stage_plan",
                columns: new[] { "project_id", "stage_code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_configuration_version_stage_configurati",
                schema: "progress",
                table: "progress_submission",
                column: "stage_configuration_version_id",
                principalSchema: "master_data_config",
                principalTable: "configuration_version",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_progress_submission_configuration_version_stage_configurati",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropTable(
                name: "progress_stage_entry",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "project_stage_plan",
                schema: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_current_stage_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_stage_plan_revision_no",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_progress_submission_stage_configuration_version_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropCheckConstraint(
                name: "ck_progress_submission_actual_source",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropCheckConstraint(
                name: "ck_progress_submission_planned_baseline",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropCheckConstraint(
                name: "ck_change_authorization_authorization_scope",
                schema: "change_request",
                table: "change_authorization");

            migrationBuilder.DropColumn(
                name: "current_stage_code",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "current_stage_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "stage_plan_revision_no",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "stage_configuration_version_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.AddCheckConstraint(
                name: "ck_progress_submission_actual_source",
                schema: "progress",
                table: "progress_submission",
                sql: "\"actual_source\" IN ('WORK_BREAKDOWN', 'DECLARED', 'NOT_AVAILABLE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_progress_submission_planned_baseline",
                schema: "progress",
                table: "progress_submission",
                sql: "planned_percent IS NULL OR baseline_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_change_authorization_authorization_scope",
                schema: "change_request",
                table: "change_authorization",
                sql: "\"authorization_scope\" IN ('REBASELINE', 'COMMITMENT_CHANGE', 'PROFILE_CHANGE', 'SCOPE_CHANGE')");
        }
    }
}
