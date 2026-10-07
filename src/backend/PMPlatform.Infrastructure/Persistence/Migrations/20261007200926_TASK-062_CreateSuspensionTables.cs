using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-062, 1 of 4: WF-09's two tables in the <c>suspension</c> schema, as the ERD (§5.12) has them. <c>suspension_request</c> carries
    /// SCR-108's indexes (indexing-strategy I-29, I-30, I-54) and a partial unique key on (project_id, request_type) over the states that are
    /// not final: at most one open suspension request and one open resumption request per project (BR-SUS-004, BR-SUS-005).
    /// <c>active_suspension</c> carries the ERD's partial unique key on project_id where ended_at IS NULL: at most one open suspension per
    /// project (BR-SUS-003, TASK-065). Foreign keys to other modules' tables are migration 2's.
    /// </summary>
    public partial class TASK062_CreateSuspensionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "suspension_request",
                schema: "suspension",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    requested_effective_date = table.Column<DateOnly>(type: "date", nullable: true),
                    planned_resumption_date = table.Column<DateOnly>(type: "date", nullable: true),
                    effected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    reason_lang = table.Column<string>(type: "char(2)", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suspension_request", x => x.id);
                    table.CheckConstraint("ck_suspension_request_effected", "(status = 'EFFECTED') = (effected_at IS NOT NULL)");
                    table.CheckConstraint("ck_suspension_request_effective_date", "status IN ('DRAFT', 'RETURNED') OR requested_effective_date IS NOT NULL");
                    table.CheckConstraint("ck_suspension_request_planned_resumption_date", "planned_resumption_date IS NULL OR (request_type = 'SUSPEND' AND (requested_effective_date IS NULL OR planned_resumption_date > requested_effective_date))");
                    table.CheckConstraint("ck_suspension_request_reason_lang", "\"reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_suspension_request_request_type", "\"request_type\" IN ('SUSPEND', 'RESUME')");
                    table.CheckConstraint("ck_suspension_request_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_suspension_request_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED', 'REJECTED', 'WITHDRAWN', 'EFFECTED')");
                    table.CheckConstraint("ck_suspension_request_submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "active_suspension",
                schema: "suspension",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suspension_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resumption_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_active_suspension", x => x.id);
                    table.CheckConstraint("ck_active_suspension_ended", "(ended_at IS NULL) = (resumption_request_id IS NULL) AND (ended_at IS NULL OR ended_at >= started_at)");
                    table.ForeignKey(
                        name: "fk_active_suspension_suspension_request_resumption_request_id",
                        column: x => x.resumption_request_id,
                        principalSchema: "suspension",
                        principalTable: "suspension_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_active_suspension_suspension_request_suspension_request_id",
                        column: x => x.suspension_request_id,
                        principalSchema: "suspension",
                        principalTable: "suspension_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_active_suspension_open_project_id",
                schema: "suspension",
                table: "active_suspension",
                column: "project_id",
                unique: true,
                filter: "ended_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_active_suspension_project_id_started_at_id",
                schema: "suspension",
                table: "active_suspension",
                columns: new[] { "project_id", "started_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_active_suspension_resumption_request_id",
                schema: "suspension",
                table: "active_suspension",
                column: "resumption_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_active_suspension_suspension_request_id",
                schema: "suspension",
                table: "active_suspension",
                column: "suspension_request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suspension_request_open_project_id_request_type",
                schema: "suspension",
                table: "suspension_request",
                columns: new[] { "project_id", "request_type" },
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED')");

            migrationBuilder.CreateIndex(
                name: "ix_suspension_request_project_id_updated_at_id",
                schema: "suspension",
                table: "suspension_request",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_suspension_request_status_updated_at_id",
                schema: "suspension",
                table: "suspension_request",
                columns: new[] { "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_suspension_request_updated_at_id",
                schema: "suspension",
                table: "suspension_request",
                columns: new[] { "updated_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "active_suspension",
                schema: "suspension");

            migrationBuilder.DropTable(
                name: "suspension_request",
                schema: "suspension");
        }
    }
}
