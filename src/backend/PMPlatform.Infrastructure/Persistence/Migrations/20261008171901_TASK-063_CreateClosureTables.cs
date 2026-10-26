using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-063, 2 of 5: WF-10's four tables in the <c>closure</c> schema, as the ERD (§5.13) has them, with three additions recorded in
    /// closure.md: a closure case's completion is optional (the terminal path from SUSPENDED has none), an obligation is recorded against a
    /// completion case or a terminal closure case (exactly one), and a readiness record keeps how many records blocked it. Each case
    /// table carries the ERD's partial unique key of one EFFECTED case per project, one of one open case per project (CLO-CC-05, CLO-CC-06),
    /// and SCR-111's indexes. Foreign keys to other modules' tables are migration 3's.
    /// </summary>
    public partial class TASK063_CreateClosureTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "completion_case",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    actual_project_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    completion_narrative = table.Column<string>(type: "text", nullable: true),
                    completion_narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    effected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_completion_case", x => x.id);
                    table.CheckConstraint("ck_completion_case_completion_narrative_lang", "\"completion_narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_completion_case_completion_narrative_pair", "(\"completion_narrative\" IS NULL) = (\"completion_narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_completion_case_effected", "(status = 'EFFECTED') = (effected_at IS NOT NULL)");
                    table.CheckConstraint("ck_completion_case_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_completion_case_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED', 'REJECTED', 'WITHDRAWN', 'EFFECTED')");
                    table.CheckConstraint("ck_completion_case_submission", "status IN ('DRAFT', 'RETURNED', 'WITHDRAWN') OR (actual_project_completion_date IS NOT NULL AND completion_narrative IS NOT NULL)");
                    table.CheckConstraint("ck_completion_case_submitted", "status = 'WITHDRAWN' OR (status = 'DRAFT') = (submitted_at IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "closure_case",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completion_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closure_narrative = table.Column<string>(type: "text", nullable: true),
                    closure_narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    effected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_closure_case", x => x.id);
                    table.CheckConstraint("ck_closure_case_closure_narrative_lang", "\"closure_narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_closure_case_closure_narrative_pair", "(\"closure_narrative\" IS NULL) = (\"closure_narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_closure_case_effected", "(status = 'EFFECTED') = (effected_at IS NOT NULL)");
                    table.CheckConstraint("ck_closure_case_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_closure_case_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED', 'REJECTED', 'WITHDRAWN', 'EFFECTED')");
                    table.CheckConstraint("ck_closure_case_submission", "status IN ('DRAFT', 'RETURNED', 'WITHDRAWN') OR closure_narrative IS NOT NULL");
                    table.CheckConstraint("ck_closure_case_submitted", "status = 'WITHDRAWN' OR (status = 'DRAFT') = (submitted_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_closure_case_completion_case_completion_case_id",
                        column: x => x.completion_case_id,
                        principalSchema: "closure",
                        principalTable: "completion_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "post_project_obligation",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    completion_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    description_lang = table.Column<string>(type: "char(2)", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    satisfied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_post_project_obligation", x => x.id);
                    table.CheckConstraint("ck_post_project_obligation_case", "(completion_case_id IS NULL) <> (closure_case_id IS NULL)");
                    table.CheckConstraint("ck_post_project_obligation_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_post_project_obligation_description_pair", "(\"description\" IS NULL) = (\"description_lang\" IS NULL)");
                    table.CheckConstraint("ck_post_project_obligation_satisfied", "(status = 'SATISFIED') = (satisfied_at IS NOT NULL)");
                    table.CheckConstraint("ck_post_project_obligation_status", "\"status\" IN ('OPEN', 'IN_PROGRESS', 'SATISFIED', 'WAIVED', 'CANCELLED')");
                    table.CheckConstraint("ck_post_project_obligation_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_post_project_obligation_closure_case_closure_case_id",
                        column: x => x.closure_case_id,
                        principalSchema: "closure",
                        principalTable: "closure_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_post_project_obligation_completion_case_completion_case_id",
                        column: x => x.completion_case_id,
                        principalSchema: "closure",
                        principalTable: "completion_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "readiness_check",
                schema: "closure",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    completion_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closure_case_id = table.Column<Guid>(type: "uuid", nullable: true),
                    check_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    result = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    blocking_count = table.Column<int>(type: "integer", nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    detail_lang = table.Column<string>(type: "char(2)", nullable: true),
                    waived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_readiness_check", x => x.id);
                    table.CheckConstraint("ck_readiness_check_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_readiness_check_blocking_count", "blocking_count >= 0 AND (result <> 'PASS' OR blocking_count = 0) AND (result <> 'FAIL' OR blocking_count > 0)");
                    table.CheckConstraint("ck_readiness_check_case", "(completion_case_id IS NULL) <> (closure_case_id IS NULL)");
                    table.CheckConstraint("ck_readiness_check_check_code", "\"check_code\" IN ('DECISIONS_SETTLED', 'TASKS_DISPOSITIONED', 'SCHEDULE_RECONCILED', 'MILESTONES_DISPOSITIONED', 'RISKS_DISPOSITIONED', 'ISSUES_DISPOSITIONED', 'CHANGES_DISPOSITIONED', 'SUSPENSION_REQUESTS_SETTLED', 'PROGRESS_REPORTED', 'FINANCIALS_SETTLED', 'OBLIGATIONS_OWNED', 'OBLIGATIONS_SATISFIED')");
                    table.CheckConstraint("ck_readiness_check_detail_lang", "\"detail_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_readiness_check_detail_pair", "(\"detail\" IS NULL) = (\"detail_lang\" IS NULL)");
                    table.CheckConstraint("ck_readiness_check_result", "\"result\" IN ('PASS', 'FAIL', 'WAIVED')");
                    table.CheckConstraint("ck_readiness_check_waiver", "(result = 'WAIVED') = (waived_by_user_id IS NOT NULL) AND (result = 'WAIVED') = (detail IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_readiness_check_closure_case_closure_case_id",
                        column: x => x.closure_case_id,
                        principalSchema: "closure",
                        principalTable: "closure_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_readiness_check_completion_case_completion_case_id",
                        column: x => x.completion_case_id,
                        principalSchema: "closure",
                        principalTable: "completion_case",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_completion_case_id",
                schema: "closure",
                table: "closure_case",
                column: "completion_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_effected_project_id",
                schema: "closure",
                table: "closure_case",
                column: "project_id",
                unique: true,
                filter: "status = 'EFFECTED'");

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_open_project_id",
                schema: "closure",
                table: "closure_case",
                column: "project_id",
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED')");

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_project_id_updated_at_id",
                schema: "closure",
                table: "closure_case",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_status_updated_at_id",
                schema: "closure",
                table: "closure_case",
                columns: new[] { "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_completion_case_effected_project_id",
                schema: "closure",
                table: "completion_case",
                column: "project_id",
                unique: true,
                filter: "status = 'EFFECTED'");

            migrationBuilder.CreateIndex(
                name: "ix_completion_case_open_project_id",
                schema: "closure",
                table: "completion_case",
                column: "project_id",
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED')");

            migrationBuilder.CreateIndex(
                name: "ix_completion_case_project_id_updated_at_id",
                schema: "closure",
                table: "completion_case",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_completion_case_status_updated_at_id",
                schema: "closure",
                table: "completion_case",
                columns: new[] { "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_post_project_obligation_closure_case_id",
                schema: "closure",
                table: "post_project_obligation",
                column: "closure_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_project_obligation_completion_case_id",
                schema: "closure",
                table: "post_project_obligation",
                column: "completion_case_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_project_obligation_project_id_updated_at_id",
                schema: "closure",
                table: "post_project_obligation",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_closure_case_id_evaluated_at",
                schema: "closure",
                table: "readiness_check",
                columns: new[] { "closure_case_id", "evaluated_at" });

            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_completion_case_id_evaluated_at",
                schema: "closure",
                table: "readiness_check",
                columns: new[] { "completion_case_id", "evaluated_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "post_project_obligation",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "readiness_check",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "closure_case",
                schema: "closure");

            migrationBuilder.DropTable(
                name: "completion_case",
                schema: "closure");
        }
    }
}
