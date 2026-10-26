using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-063, 3 of 5: the foreign keys from <c>closure</c> to the tables of other modules that ERD D-14 makes referenceable —
    /// <c>project.project</c> from the cases and obligations (edge 8) and <c>identity_access.user</c> from a case's requester, an
    /// obligation's owner and a waiver's author — with their indexes.
    /// </summary>
    public partial class TASK063_AddClosureCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_readiness_check_waived_by_user_id",
                schema: "closure",
                table: "readiness_check",
                column: "waived_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_post_project_obligation_owner_user_id",
                schema: "closure",
                table: "post_project_obligation",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_completion_case_requested_by_user_id",
                schema: "closure",
                table: "completion_case",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_closure_case_requested_by_user_id",
                schema: "closure",
                table: "closure_case",
                column: "requested_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_closure_case_project_project_id",
                schema: "closure",
                table: "closure_case",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_closure_case_user_requested_by_user_id",
                schema: "closure",
                table: "closure_case",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_completion_case_project_project_id",
                schema: "closure",
                table: "completion_case",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_completion_case_user_requested_by_user_id",
                schema: "closure",
                table: "completion_case",
                column: "requested_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_post_project_obligation_project_project_id",
                schema: "closure",
                table: "post_project_obligation",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_post_project_obligation_user_owner_user_id",
                schema: "closure",
                table: "post_project_obligation",
                column: "owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_readiness_check_user_waived_by_user_id",
                schema: "closure",
                table: "readiness_check",
                column: "waived_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_closure_case_project_project_id",
                schema: "closure",
                table: "closure_case");

            migrationBuilder.DropForeignKey(
                name: "fk_closure_case_user_requested_by_user_id",
                schema: "closure",
                table: "closure_case");

            migrationBuilder.DropForeignKey(
                name: "fk_completion_case_project_project_id",
                schema: "closure",
                table: "completion_case");

            migrationBuilder.DropForeignKey(
                name: "fk_completion_case_user_requested_by_user_id",
                schema: "closure",
                table: "completion_case");

            migrationBuilder.DropForeignKey(
                name: "fk_post_project_obligation_project_project_id",
                schema: "closure",
                table: "post_project_obligation");

            migrationBuilder.DropForeignKey(
                name: "fk_post_project_obligation_user_owner_user_id",
                schema: "closure",
                table: "post_project_obligation");

            migrationBuilder.DropForeignKey(
                name: "fk_readiness_check_user_waived_by_user_id",
                schema: "closure",
                table: "readiness_check");

            migrationBuilder.DropIndex(
                name: "ix_readiness_check_waived_by_user_id",
                schema: "closure",
                table: "readiness_check");

            migrationBuilder.DropIndex(
                name: "ix_post_project_obligation_owner_user_id",
                schema: "closure",
                table: "post_project_obligation");

            migrationBuilder.DropIndex(
                name: "ix_completion_case_requested_by_user_id",
                schema: "closure",
                table: "completion_case");

            migrationBuilder.DropIndex(
                name: "ix_closure_case_requested_by_user_id",
                schema: "closure",
                table: "closure_case");
        }
    }
}
