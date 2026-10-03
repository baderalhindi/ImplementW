using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 3 of 6: WF-05's table, ERD §5.8 <c>milestone.milestone_achievement</c> — one row per revision of an achievement
    /// claim — with the workflow's facts as CHECK constraints, the ERD's partial unique index of one ACCEPTED revision per
    /// milestone, and one of one open revision per milestone. The schema is TASK-024's. The foreign keys to other modules'
    /// tables, the shared milestone row among them, follow in 4 of 6.
    /// </summary>
    public partial class TASK050_CreateMilestoneAchievementTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "milestone_achievement",
                schema: "milestone",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    claimed_achievement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    accepted_actual_achievement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_by_achievement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    narrative = table.Column<string>(type: "text", nullable: true),
                    return_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    return_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestone_achievement", x => x.id);
                    table.CheckConstraint("ck_milestone_achievement_accepted", "(status IN ('ACCEPTED', 'SUPERSEDED')) = (accepted_actual_achievement_date IS NOT NULL)");
                    table.CheckConstraint("ck_milestone_achievement_narrative_lang", "\"narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_milestone_achievement_narrative_pair", "(\"narrative\" IS NULL) = (\"narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_milestone_achievement_return_reason", "return_reason IS NULL OR status = 'RETURNED'");
                    table.CheckConstraint("ck_milestone_achievement_return_reason_lang", "\"return_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_milestone_achievement_return_reason_pair", "(\"return_reason\" IS NULL) = (\"return_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_milestone_achievement_reviewed", "(status IN ('RETURNED', 'ACCEPTED', 'SUPERSEDED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_milestone_achievement_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_milestone_achievement_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'RETURNED', 'ACCEPTED', 'SUPERSEDED')");
                    table.CheckConstraint("ck_milestone_achievement_submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
                    table.CheckConstraint("ck_milestone_achievement_superseded", "(status = 'SUPERSEDED') = (superseded_by_achievement_id IS NOT NULL) AND (superseded_by_achievement_id IS NULL OR superseded_by_achievement_id <> id)");
                    table.ForeignKey(
                        name: "fk_milestone_achievement_milestone_achievement_superseded_by_a",
                        column: x => x.superseded_by_achievement_id,
                        principalSchema: "milestone",
                        principalTable: "milestone_achievement",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_accepted_project_milestone_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_milestone_id",
                unique: true,
                filter: "status = 'ACCEPTED'");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_open_project_milestone_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_milestone_id",
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED')");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_project_milestone_id_revision_no",
                schema: "milestone",
                table: "milestone_achievement",
                columns: new[] { "project_milestone_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_superseded_by_achievement_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "superseded_by_achievement_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "milestone_achievement",
                schema: "milestone");
        }
    }
}
