using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-044, 1 of 3: the WF-02 tables of ERD §5.5 <c>progress</c> — <c>reporting_cycle</c>, <c>progress_submission</c>,
    /// <c>published_progress_snapshot</c>, <c>project_health_status</c> — with the foreign keys inside the schema and the
    /// register indexes I-37 and I-38. The foreign keys to other modules' tables follow in 2 of 3 (migrations README R-7).
    /// <c>periodic_update_session</c> and its items are TASK-107's (progress-update.md §1).
    /// </summary>
    public partial class TASK044_CreateProgressTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_health_status",
                schema: "progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overall_health = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actual_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    planned_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    health_rule_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_health_status", x => x.id);
                    table.CheckConstraint("ck_project_health_status_overall_health", "\"overall_health\" IN ('GREEN', 'AMBER', 'RED', 'UNKNOWN')");
                    table.CheckConstraint("ck_project_health_status_percent", "(actual_percent IS NULL OR actual_percent BETWEEN 0 AND 100) AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");
                });

            migrationBuilder.CreateTable(
                name: "reporting_cycle",
                schema: "progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reporting_cycle", x => x.id);
                    table.CheckConstraint("ck_reporting_cycle_period", "period_end >= period_start AND due_date >= period_start");
                    table.CheckConstraint("ck_reporting_cycle_status", "\"status\" IN ('OPEN', 'CLOSED')");
                });

            migrationBuilder.CreateTable(
                name: "progress_submission",
                schema: "progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporting_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actual_percent_calculated = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    actual_percent_override = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    planned_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    baseline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    narrative = table.Column<string>(type: "text", nullable: true),
                    override_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    override_reason = table.Column<string>(type: "text", nullable: true),
                    return_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    return_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_progress_submission", x => x.id);
                    table.CheckConstraint("ck_progress_submission_narrative_lang", "\"narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_progress_submission_narrative_pair", "(\"narrative\" IS NULL) = (\"narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_progress_submission_override", "(actual_percent_override IS NULL) = (override_reason IS NULL)");
                    table.CheckConstraint("ck_progress_submission_override_reason_lang", "\"override_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_progress_submission_override_reason_pair", "(\"override_reason\" IS NULL) = (\"override_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_progress_submission_percent", "actual_percent_calculated BETWEEN 0 AND 100 AND (actual_percent_override IS NULL OR actual_percent_override BETWEEN 0 AND 100) AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");
                    table.CheckConstraint("ck_progress_submission_planned_baseline", "planned_percent IS NULL OR baseline_id IS NOT NULL");
                    table.CheckConstraint("ck_progress_submission_return_reason", "(status = 'RETURNED') = (return_reason IS NOT NULL)");
                    table.CheckConstraint("ck_progress_submission_return_reason_lang", "\"return_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_progress_submission_return_reason_pair", "(\"return_reason\" IS NULL) = (\"return_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_progress_submission_reviewed", "(status IN ('RETURNED', 'PUBLISHED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_progress_submission_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_progress_submission_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'PUBLISHED')");
                    table.CheckConstraint("ck_progress_submission_submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_progress_submission_reporting_cycle_reporting_cycle_id",
                        column: x => x.reporting_cycle_id,
                        principalSchema: "progress",
                        principalTable: "reporting_cycle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "published_progress_snapshot",
                schema: "progress",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporting_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actual_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    is_overridden = table.Column<bool>(type: "boolean", nullable: false),
                    planned_percent = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    overall_health = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    schedule_health = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    financial_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    health_rule_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_progress_snapshot", x => x.id);
                    table.CheckConstraint("ck_published_progress_snapshot_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_published_progress_snapshot_financial_status", "\"financial_status\" IN ('GREEN', 'AMBER', 'RED', 'UNKNOWN')");
                    table.CheckConstraint("ck_published_progress_snapshot_overall_health", "\"overall_health\" IN ('GREEN', 'AMBER', 'RED', 'UNKNOWN')");
                    table.CheckConstraint("ck_published_progress_snapshot_percent", "actual_percent BETWEEN 0 AND 100 AND (planned_percent IS NULL OR planned_percent BETWEEN 0 AND 100)");
                    table.CheckConstraint("ck_published_progress_snapshot_schedule_health", "\"schedule_health\" IN ('GREEN', 'AMBER', 'RED', 'UNKNOWN')");
                    table.ForeignKey(
                        name: "fk_published_progress_snapshot_progress_submission_progress_su",
                        column: x => x.progress_submission_id,
                        principalSchema: "progress",
                        principalTable: "progress_submission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_published_progress_snapshot_reporting_cycle_reporting_cycle",
                        column: x => x.reporting_cycle_id,
                        principalSchema: "progress",
                        principalTable: "reporting_cycle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_project_id_submitted_at",
                schema: "progress",
                table: "progress_submission",
                columns: new[] { "project_id", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_reporting_cycle_id_revision_no",
                schema: "progress",
                table: "progress_submission",
                columns: new[] { "reporting_cycle_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_health_status_project_id",
                schema: "progress",
                table: "project_health_status",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_published_progress_snapshot_progress_submission_id",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "progress_submission_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_published_progress_snapshot_project_id_published_at",
                schema: "progress",
                table: "published_progress_snapshot",
                columns: new[] { "project_id", "published_at" });

            migrationBuilder.CreateIndex(
                name: "ix_published_progress_snapshot_reporting_cycle_id",
                schema: "progress",
                table: "published_progress_snapshot",
                column: "reporting_cycle_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reporting_cycle_project_id_period_start",
                schema: "progress",
                table: "reporting_cycle",
                columns: new[] { "project_id", "period_start" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_health_status",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "published_progress_snapshot",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "progress_submission",
                schema: "progress");

            migrationBuilder.DropTable(
                name: "reporting_cycle",
                schema: "progress");
        }
    }
}
