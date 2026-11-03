using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK111_AddProjectRpmoCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_project_import_batch_decided_by_user_id",
                schema: "project",
                table: "project_import_batch",
                column: "decided_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_import_batch_rolled_back_by_user_id",
                schema: "project",
                table: "project_import_batch",
                column: "rolled_back_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_follow_up_owner_user_id",
                schema: "project",
                table: "project",
                column: "follow_up_owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_priority_item_id",
                schema: "project",
                table: "project",
                column: "priority_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_strategic_initiative_item_id",
                schema: "project",
                table: "project",
                column: "strategic_initiative_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_strategic_portfolio_item_id",
                schema: "project",
                table: "project",
                column: "strategic_portfolio_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_master_data_item_priority_item_id",
                schema: "project",
                table: "project",
                column: "priority_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_master_data_item_strategic_initiative_item_id",
                schema: "project",
                table: "project",
                column: "strategic_initiative_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_master_data_item_strategic_portfolio_item_id",
                schema: "project",
                table: "project",
                column: "strategic_portfolio_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_follow_up_owner_user_id",
                schema: "project",
                table: "project",
                column: "follow_up_owner_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_import_batch_user_decided_by_user_id",
                schema: "project",
                table: "project_import_batch",
                column: "decided_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_import_batch_user_rolled_back_by_user_id",
                schema: "project",
                table: "project_import_batch",
                column: "rolled_back_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_master_data_item_priority_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropForeignKey(
                name: "fk_project_master_data_item_strategic_initiative_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropForeignKey(
                name: "fk_project_master_data_item_strategic_portfolio_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropForeignKey(
                name: "fk_project_user_follow_up_owner_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropForeignKey(
                name: "fk_project_import_batch_user_decided_by_user_id",
                schema: "project",
                table: "project_import_batch");

            migrationBuilder.DropForeignKey(
                name: "fk_project_import_batch_user_rolled_back_by_user_id",
                schema: "project",
                table: "project_import_batch");

            migrationBuilder.DropIndex(
                name: "ix_project_import_batch_decided_by_user_id",
                schema: "project",
                table: "project_import_batch");

            migrationBuilder.DropIndex(
                name: "ix_project_import_batch_rolled_back_by_user_id",
                schema: "project",
                table: "project_import_batch");

            migrationBuilder.DropIndex(
                name: "ix_project_follow_up_owner_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_priority_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_strategic_initiative_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_strategic_portfolio_item_id",
                schema: "project",
                table: "project");
        }
    }
}
