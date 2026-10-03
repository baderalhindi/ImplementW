using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 4 of 6: the foreign keys from <c>milestone.milestone_achievement</c> to the tables of other modules that ERD D-14
    /// makes referenceable — <c>schedule.project_milestone</c> (the shared milestone, ICD-04: WF-05 names it and holds no copy),
    /// <c>project.project</c>, <c>identity_access.user</c> (submitter and reviewer) and <c>project.project_intake</c> (ADR-014) —
    /// with their indexes.
    /// </summary>
    public partial class TASK050_AddMilestoneAchievementCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_project_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_project_intake_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_intake_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_reviewed_by_user_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_achievement_submitted_by_user_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "submitted_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_milestone_achievement_project_intake_project_intake_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_intake_id",
                principalSchema: "project",
                principalTable: "project_intake",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_milestone_achievement_project_milestone_project_milestone_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_milestone_id",
                principalSchema: "schedule",
                principalTable: "project_milestone",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_milestone_achievement_project_project_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_milestone_achievement_user_reviewed_by_user_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "reviewed_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_milestone_achievement_user_submitted_by_user_id",
                schema: "milestone",
                table: "milestone_achievement",
                column: "submitted_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_milestone_achievement_project_intake_project_intake_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropForeignKey(
                name: "fk_milestone_achievement_project_milestone_project_milestone_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropForeignKey(
                name: "fk_milestone_achievement_project_project_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropForeignKey(
                name: "fk_milestone_achievement_user_reviewed_by_user_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropForeignKey(
                name: "fk_milestone_achievement_user_submitted_by_user_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropIndex(
                name: "ix_milestone_achievement_project_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropIndex(
                name: "ix_milestone_achievement_project_intake_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropIndex(
                name: "ix_milestone_achievement_reviewed_by_user_id",
                schema: "milestone",
                table: "milestone_achievement");

            migrationBuilder.DropIndex(
                name: "ix_milestone_achievement_submitted_by_user_id",
                schema: "milestone",
                table: "milestone_achievement");
        }
    }
}
