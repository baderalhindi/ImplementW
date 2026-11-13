using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddProposedProjectManager : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "proposed_project_manager_user_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_proposed_project_manager_user_id",
                schema: "project",
                table: "project",
                column: "proposed_project_manager_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_proposed_project_manager_user_id",
                schema: "project",
                table: "project",
                column: "proposed_project_manager_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_user_proposed_project_manager_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_proposed_project_manager_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "proposed_project_manager_user_id",
                schema: "project",
                table: "project");
        }
    }
}
