using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 2 of 6: the foreign keys from <c>schedule.project_milestone</c> to the tables of other modules that ERD D-14
    /// makes referenceable — <c>project.project</c> and <c>master_data_config.master_data_item</c> (the milestone category) —
    /// with their indexes.
    /// </summary>
    public partial class TASK050_AddProjectMilestoneCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_project_milestone_milestone_category_item_id",
                schema: "schedule",
                table: "project_milestone",
                column: "milestone_category_item_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_milestone_master_data_item_milestone_category_item_",
                schema: "schedule",
                table: "project_milestone",
                column: "milestone_category_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_milestone_project_project_id",
                schema: "schedule",
                table: "project_milestone",
                column: "project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_milestone_master_data_item_milestone_category_item_",
                schema: "schedule",
                table: "project_milestone");

            migrationBuilder.DropForeignKey(
                name: "fk_project_milestone_project_project_id",
                schema: "schedule",
                table: "project_milestone");

            migrationBuilder.DropIndex(
                name: "ix_project_milestone_milestone_category_item_id",
                schema: "schedule",
                table: "project_milestone");
        }
    }
}
