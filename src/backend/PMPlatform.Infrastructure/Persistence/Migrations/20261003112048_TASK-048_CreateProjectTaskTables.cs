using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-048, 1 of 3: the WF-04 tables of ERD §5.7 <c>project_task</c> — <c>project_task</c> (tasks and subtasks),
    /// <c>task_dependency</c> and <c>activity_execution_progress</c> — with the foreign keys inside the schema, the state
    /// machine's facts as CHECK constraints, and indexes I-33 and I-34. The foreign keys to other modules' tables follow in 2 of 3
    /// (migrations README R-7).
    /// </summary>
    public partial class TASK048_CreateProjectTaskTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activity_execution_progress",
                schema: "project_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_activity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actual_percent_complete = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    computed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_execution_progress", x => x.id);
                    table.CheckConstraint("ck_activity_execution_progress_percent", "actual_percent_complete BETWEEN 0 AND 100");
                });

            migrationBuilder.CreateTable(
                name: "project_task",
                schema: "project_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    priority_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    planned_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_finish_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_duration_days = table.Column<int>(type: "integer", nullable: false),
                    actual_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_finish_date = table.Column<DateOnly>(type: "date", nullable: true),
                    actual_percent_complete = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reopened_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    blocked_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    blocked_reason = table.Column<string>(type: "text", nullable: true),
                    description_lang = table.Column<string>(type: "char(2)", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_task", x => x.id);
                    table.CheckConstraint("ck_project_task_actual", "actual_finish_date IS NULL OR (actual_start_date IS NOT NULL AND actual_finish_date >= actual_start_date)");
                    table.CheckConstraint("ck_project_task_blocked", "(status = 'BLOCKED') = (blocked_reason IS NOT NULL)");
                    table.CheckConstraint("ck_project_task_blocked_reason_lang", "\"blocked_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_task_blocked_reason_pair", "(\"blocked_reason\" IS NULL) = (\"blocked_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_task_completed", "(status = 'COMPLETED') = (completed_at IS NOT NULL) AND (status = 'COMPLETED') = (actual_finish_date IS NOT NULL)");
                    table.CheckConstraint("ck_project_task_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_task_description_pair", "(\"description\" IS NULL) = (\"description_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_task_hierarchy", "parent_task_id IS NULL OR parent_task_id <> id");
                    table.CheckConstraint("ck_project_task_percent", "actual_percent_complete IS NULL OR actual_percent_complete BETWEEN 0 AND 100");
                    table.CheckConstraint("ck_project_task_planned", "planned_finish_date >= planned_start_date AND planned_duration_days = planned_finish_date - planned_start_date + 1");
                    table.CheckConstraint("ck_project_task_reopened_count", "reopened_count >= 0");
                    table.CheckConstraint("ck_project_task_started", "(status = 'NOT_STARTED' AND actual_start_date IS NULL) OR (status IN ('IN_PROGRESS', 'COMPLETED') AND actual_start_date IS NOT NULL) OR status IN ('BLOCKED', 'CANCELLED')");
                    table.CheckConstraint("ck_project_task_status", "\"status\" IN ('NOT_STARTED', 'IN_PROGRESS', 'BLOCKED', 'COMPLETED', 'CANCELLED')");
                    table.CheckConstraint("ck_project_task_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_task_project_task_parent_task_id",
                        column: x => x.parent_task_id,
                        principalSchema: "project_task",
                        principalTable: "project_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_dependency",
                schema: "project_task",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependency_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_dependency", x => x.id);
                    table.CheckConstraint("ck_task_dependency_dependency_type", "\"dependency_type\" IN ('FS', 'SS', 'FF', 'SF')");
                    table.CheckConstraint("ck_task_dependency_ends", "predecessor_task_id <> successor_task_id");
                    table.ForeignKey(
                        name: "fk_task_dependency_project_task_predecessor_task_id",
                        column: x => x.predecessor_task_id,
                        principalSchema: "project_task",
                        principalTable: "project_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_dependency_project_task_successor_task_id",
                        column: x => x.successor_task_id,
                        principalSchema: "project_task",
                        principalTable: "project_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_execution_progress_schedule_activity_id",
                schema: "project_task",
                table: "activity_execution_progress",
                column: "schedule_activity_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_task_assignee_user_id_status_planned_finish_date",
                schema: "project_task",
                table: "project_task",
                columns: new[] { "assignee_user_id", "status", "planned_finish_date" });

            migrationBuilder.CreateIndex(
                name: "ix_project_task_parent_task_id",
                schema: "project_task",
                table: "project_task",
                column: "parent_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_task_project_id_status_planned_finish_date",
                schema: "project_task",
                table: "project_task",
                columns: new[] { "project_id", "status", "planned_finish_date" });

            migrationBuilder.CreateIndex(
                name: "ix_task_dependency_predecessor_task_id_successor_task_id",
                schema: "project_task",
                table: "task_dependency",
                columns: new[] { "predecessor_task_id", "successor_task_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_dependency_successor_task_id",
                schema: "project_task",
                table: "task_dependency",
                column: "successor_task_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_execution_progress",
                schema: "project_task");

            migrationBuilder.DropTable(
                name: "task_dependency",
                schema: "project_task");

            migrationBuilder.DropTable(
                name: "project_task",
                schema: "project_task");
        }
    }
}
