using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-046, 1 of 4: the WF-03 tables of ERD §5.6 <c>schedule</c> — <c>project_schedule</c>, <c>schedule_activity</c>,
    /// <c>schedule_dependency</c>, <c>project_baseline</c>, <c>baseline_activity</c>, <c>baseline_dependency</c>,
    /// <c>schedule_health_status</c> — with the foreign keys inside the schema, and the partial unique index that allows one
    /// ACTIVE baseline per project. The foreign keys to other modules' tables follow in 2 of 4 (migrations README R-7).
    /// <c>project_milestone</c> and <c>baseline_milestone</c> are TASK-050's (schedule-baseline.md §1).
    /// </summary>
    public partial class TASK046_CreateScheduleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_baseline",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    baseline_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_by_baseline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    change_authorization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    declared_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    baseline_finish_date = table.Column<DateOnly>(type: "date", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    declared_scope_lang = table.Column<string>(type: "char(2)", nullable: true),
                    declared_scope = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_baseline", x => x.id);
                    table.CheckConstraint("ck_project_baseline_activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
                    table.CheckConstraint("ck_project_baseline_baseline_type", "\"baseline_type\" IN ('APPROVED', 'DECLARED')");
                    table.CheckConstraint("ck_project_baseline_declared", "(baseline_type = 'DECLARED') = (project_intake_id IS NOT NULL) AND (baseline_type = 'DECLARED') = (declared_end_date IS NOT NULL) AND (baseline_type = 'DECLARED' OR declared_scope IS NULL) AND (baseline_type = 'APPROVED' OR change_authorization_id IS NULL)");
                    table.CheckConstraint("ck_project_baseline_declared_scope_lang", "\"declared_scope_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_baseline_declared_scope_pair", "(\"declared_scope\" IS NULL) = (\"declared_scope_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_baseline_numbers", "version_no >= 1 AND revision_no >= 1");
                    table.CheckConstraint("ck_project_baseline_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'ACTIVE', 'SUPERSEDED', 'REJECTED', 'WITHDRAWN')");
                    table.CheckConstraint("ck_project_baseline_superseded", "(status = 'SUPERSEDED') = (superseded_at IS NOT NULL) AND (superseded_at IS NULL) = (superseded_by_baseline_id IS NULL) AND (superseded_by_baseline_id IS NULL OR superseded_by_baseline_id <> id)");
                    table.ForeignKey(
                        name: "fk_project_baseline_project_baseline_superseded_by_baseline_id",
                        column: x => x.superseded_by_baseline_id,
                        principalSchema: "schedule",
                        principalTable: "project_baseline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_schedule",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calendar_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_schedule", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schedule_health_status",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_baseline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    schedule_health = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    finish_variance_days = table.Column<int>(type: "integer", nullable: true),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    health_rule_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_health_status", x => x.id);
                    table.CheckConstraint("ck_schedule_health_status_schedule_health", "\"schedule_health\" IN ('GREEN', 'AMBER', 'RED', 'UNKNOWN')");
                    table.CheckConstraint("ck_schedule_health_status_variance", "finish_variance_days IS NULL OR project_baseline_id IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_schedule_health_status_project_baseline_project_baseline_id",
                        column: x => x.project_baseline_id,
                        principalSchema: "schedule",
                        principalTable: "project_baseline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_activity",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    wbs_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    activity_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_finish_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_duration_days = table.Column<int>(type: "integer", nullable: false),
                    forecast_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    forecast_finish_date = table.Column<DateOnly>(type: "date", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_finish_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    name_lang = table.Column<string>(type: "char(2)", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_activity", x => x.id);
                    table.CheckConstraint("ck_schedule_activity_activity_kind", "\"activity_kind\" IN ('SUMMARY', 'ACTIVITY')");
                    table.CheckConstraint("ck_schedule_activity_actual", "actual_finish_date IS NULL OR (actual_start_date IS NOT NULL AND actual_finish_date >= actual_start_date)");
                    table.CheckConstraint("ck_schedule_activity_forecast", "forecast_finish_date >= forecast_start_date");
                    table.CheckConstraint("ck_schedule_activity_hierarchy", "parent_activity_id IS NULL OR parent_activity_id <> id");
                    table.CheckConstraint("ck_schedule_activity_name_lang", "\"name_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_schedule_activity_planned", "planned_duration_days >= 1 AND planned_finish_date >= planned_start_date");
                    table.CheckConstraint("ck_schedule_activity_status", "\"status\" IN ('PLANNED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_schedule_activity_project_schedule_project_schedule_id",
                        column: x => x.project_schedule_id,
                        principalSchema: "schedule",
                        principalTable: "project_schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_schedule_activity_schedule_activity_parent_activity_id",
                        column: x => x.parent_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "baseline_activity",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_finish_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_duration_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baseline_activity", x => x.id);
                    table.CheckConstraint("ck_baseline_activity_activity_kind", "\"activity_kind\" IN ('SUMMARY', 'ACTIVITY')");
                    table.CheckConstraint("ck_baseline_activity_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_baseline_activity_planned", "planned_duration_days >= 1 AND planned_finish_date >= planned_start_date");
                    table.ForeignKey(
                        name: "fk_baseline_activity_project_baseline_project_baseline_id",
                        column: x => x.project_baseline_id,
                        principalSchema: "schedule",
                        principalTable: "project_baseline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baseline_activity_schedule_activity_parent_activity_id",
                        column: x => x.parent_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baseline_activity_schedule_activity_schedule_activity_id",
                        column: x => x.schedule_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "baseline_dependency",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependency_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lag_days = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baseline_dependency", x => x.id);
                    table.CheckConstraint("ck_baseline_dependency_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_baseline_dependency_dependency_type", "\"dependency_type\" IN ('FS', 'SS', 'FF')");
                    table.CheckConstraint("ck_baseline_dependency_ends", "predecessor_activity_id <> successor_activity_id");
                    table.CheckConstraint("ck_baseline_dependency_lag_days", "lag_days >= 0");
                    table.ForeignKey(
                        name: "fk_baseline_dependency_project_baseline_project_baseline_id",
                        column: x => x.project_baseline_id,
                        principalSchema: "schedule",
                        principalTable: "project_baseline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baseline_dependency_schedule_activity_predecessor_activity_",
                        column: x => x.predecessor_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baseline_dependency_schedule_activity_successor_activity_id",
                        column: x => x.successor_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "schedule_dependency",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependency_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    lag_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_dependency", x => x.id);
                    table.CheckConstraint("ck_schedule_dependency_dependency_type", "\"dependency_type\" IN ('FS', 'SS', 'FF')");
                    table.CheckConstraint("ck_schedule_dependency_ends", "predecessor_activity_id <> successor_activity_id");
                    table.CheckConstraint("ck_schedule_dependency_lag_days", "lag_days >= 0");
                    table.ForeignKey(
                        name: "fk_schedule_dependency_schedule_activity_predecessor_activity_",
                        column: x => x.predecessor_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_schedule_dependency_schedule_activity_successor_activity_id",
                        column: x => x.successor_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_baseline_activity_parent_activity_id",
                schema: "schedule",
                table: "baseline_activity",
                column: "parent_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_baseline_activity_project_baseline_id_schedule_activity_id",
                schema: "schedule",
                table: "baseline_activity",
                columns: new[] { "project_baseline_id", "schedule_activity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_baseline_activity_schedule_activity_id",
                schema: "schedule",
                table: "baseline_activity",
                column: "schedule_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_baseline_dependency_predecessor_activity_id",
                schema: "schedule",
                table: "baseline_dependency",
                column: "predecessor_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_baseline_dependency_project_baseline_id_predecessor_activit",
                schema: "schedule",
                table: "baseline_dependency",
                columns: new[] { "project_baseline_id", "predecessor_activity_id", "successor_activity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_baseline_dependency_successor_activity_id",
                schema: "schedule",
                table: "baseline_dependency",
                column: "successor_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_baseline_active_project_id",
                schema: "schedule",
                table: "project_baseline",
                column: "project_id",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_project_baseline_project_id_version_no",
                schema: "schedule",
                table: "project_baseline",
                columns: new[] { "project_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_baseline_project_intake_id",
                schema: "schedule",
                table: "project_baseline",
                column: "project_intake_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_baseline_superseded_by_baseline_id",
                schema: "schedule",
                table: "project_baseline",
                column: "superseded_by_baseline_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_schedule_project_id",
                schema: "schedule",
                table: "project_schedule",
                column: "project_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_activity_parent_activity_id",
                schema: "schedule",
                table: "schedule_activity",
                column: "parent_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_activity_project_schedule_id_wbs_code",
                schema: "schedule",
                table: "schedule_activity",
                columns: new[] { "project_schedule_id", "wbs_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_dependency_predecessor_activity_id_successor_activ",
                schema: "schedule",
                table: "schedule_dependency",
                columns: new[] { "predecessor_activity_id", "successor_activity_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_schedule_dependency_successor_activity_id",
                schema: "schedule",
                table: "schedule_dependency",
                column: "successor_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_health_status_project_baseline_id",
                schema: "schedule",
                table: "schedule_health_status",
                column: "project_baseline_id");

            migrationBuilder.CreateIndex(
                name: "ix_schedule_health_status_project_id",
                schema: "schedule",
                table: "schedule_health_status",
                column: "project_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "baseline_activity",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "baseline_dependency",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "schedule_dependency",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "schedule_health_status",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "schedule_activity",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "project_baseline",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "project_schedule",
                schema: "schedule");
        }
    }
}
