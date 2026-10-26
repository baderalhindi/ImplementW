using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-073, 2 of 2: the two <c>business_activity_entry</c> foreign keys that reference other modules' referenceable tables
    /// (ERD D-14) — the project to <c>project.project</c>, the actor to <c>identity_access.user</c> — with the actor's index (the
    /// project's is led by I-48).
    /// </summary>
    public partial class TASK073_AddActivityProjectionCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_business_activity_entry_actor_user_id",
                schema: "audit_activity",
                table: "business_activity_entry",
                column: "actor_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_business_activity_entry_project_project_id",
                schema: "audit_activity",
                table: "business_activity_entry",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_business_activity_entry_user_actor_user_id",
                schema: "audit_activity",
                table: "business_activity_entry",
                column: "actor_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_business_activity_entry_project_project_id",
                schema: "audit_activity",
                table: "business_activity_entry");

            migrationBuilder.DropForeignKey(
                name: "fk_business_activity_entry_user_actor_user_id",
                schema: "audit_activity",
                table: "business_activity_entry");

            migrationBuilder.DropIndex(
                name: "ix_business_activity_entry_actor_user_id",
                schema: "audit_activity",
                table: "business_activity_entry");
        }
    }
}
