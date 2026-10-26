using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 1 of 6: WF-03's side of the shared milestone (ICD-04), ERD §5.6 — <c>schedule.project_milestone</c>, the one row
    /// per milestone that WF-03 and WF-05 both name, and <c>schedule.baseline_milestone</c>, a baseline's copy of its date — with
    /// the foreign keys inside the schema and indexes I-35 and I-36. The foreign keys to other modules' tables follow in 2 of 6
    /// (migrations README R-7).
    /// </summary>
    public partial class TASK050_CreateProjectMilestoneTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_milestone",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_schedule_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schedule_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    milestone_category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forecast_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_milestone", x => x.id);
                    table.CheckConstraint("ck_project_milestone_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_project_milestone_status", "\"status\" IN ('PLANNED', 'ACHIEVED', 'CANCELLED')");
                    table.CheckConstraint("ck_project_milestone_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_milestone_project_schedule_project_schedule_id",
                        column: x => x.project_schedule_id,
                        principalSchema: "schedule",
                        principalTable: "project_schedule",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_milestone_schedule_activity_schedule_activity_id",
                        column: x => x.schedule_activity_id,
                        principalSchema: "schedule",
                        principalTable: "schedule_activity",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "baseline_milestone",
                schema: "schedule",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_baseline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    planned_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_baseline_milestone", x => x.id);
                    table.CheckConstraint("ck_baseline_milestone_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.ForeignKey(
                        name: "fk_baseline_milestone_project_baseline_project_baseline_id",
                        column: x => x.project_baseline_id,
                        principalSchema: "schedule",
                        principalTable: "project_baseline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_baseline_milestone_project_milestone_project_milestone_id",
                        column: x => x.project_milestone_id,
                        principalSchema: "schedule",
                        principalTable: "project_milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_baseline_milestone_project_baseline_id_project_milestone_id",
                schema: "schedule",
                table: "baseline_milestone",
                columns: new[] { "project_baseline_id", "project_milestone_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_baseline_milestone_project_milestone_id",
                schema: "schedule",
                table: "baseline_milestone",
                column: "project_milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_milestone_project_id_forecast_date",
                schema: "schedule",
                table: "project_milestone",
                columns: new[] { "project_id", "forecast_date" });

            migrationBuilder.CreateIndex(
                name: "ix_project_milestone_project_schedule_id",
                schema: "schedule",
                table: "project_milestone",
                column: "project_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_milestone_schedule_activity_id",
                schema: "schedule",
                table: "project_milestone",
                column: "schedule_activity_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_milestone_status_forecast_date",
                schema: "schedule",
                table: "project_milestone",
                columns: new[] { "status", "forecast_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "baseline_milestone",
                schema: "schedule");

            migrationBuilder.DropTable(
                name: "project_milestone",
                schema: "schedule");
        }
    }
}
