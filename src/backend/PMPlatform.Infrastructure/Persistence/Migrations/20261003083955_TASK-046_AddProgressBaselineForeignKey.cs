using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-046, 3 of 4: <c>progress.progress_submission.baseline_id</c>'s key to <c>schedule.project_baseline</c>, which
    /// TASK-044 left for this task because the table did not exist (progress-update.md F-1). A migration of the
    /// <c>progress</c> schema only (migrations README R-7).
    /// </summary>
    public partial class TASK046_AddProgressBaselineForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_progress_submission_baseline_id",
                schema: "progress",
                table: "progress_submission",
                column: "baseline_id");

            migrationBuilder.AddForeignKey(
                name: "fk_progress_submission_project_baseline_baseline_id",
                schema: "progress",
                table: "progress_submission",
                column: "baseline_id",
                principalSchema: "schedule",
                principalTable: "project_baseline",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_progress_submission_project_baseline_baseline_id",
                schema: "progress",
                table: "progress_submission");

            migrationBuilder.DropIndex(
                name: "ix_progress_submission_baseline_id",
                schema: "progress",
                table: "progress_submission");
        }
    }
}
