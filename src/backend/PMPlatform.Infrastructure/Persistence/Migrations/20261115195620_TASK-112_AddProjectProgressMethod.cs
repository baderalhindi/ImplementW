using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddProjectProgressMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "progress_method",
                schema: "project",
                table: "project",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_progress_method",
                schema: "project",
                table: "project",
                sql: "\"progress_method\" IN ('SCHEDULE', 'STAGES')");

            // Review round 8, point 02: the stage-plan step sets the stage method, so a project that already has a plan is on it. The others,
            // imported or registered before stages, keep their declared figure until their method is set.
            migrationBuilder.Sql("UPDATE project.project SET progress_method = 'STAGES' WHERE stage_plan_revision_no > 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_project_progress_method",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "progress_method",
                schema: "project",
                table: "project");
        }
    }
}
