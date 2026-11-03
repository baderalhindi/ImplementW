using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK111_AddMasterDataParentCatalogue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_catalogue_id",
                schema: "master_data_config",
                table: "master_data_catalogue",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_master_data_catalogue_parent_catalogue_id",
                schema: "master_data_config",
                table: "master_data_catalogue",
                column: "parent_catalogue_id");

            migrationBuilder.AddForeignKey(
                name: "fk_master_data_catalogue_master_data_catalogue_parent_catalogu",
                schema: "master_data_config",
                table: "master_data_catalogue",
                column: "parent_catalogue_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_catalogue",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_master_data_catalogue_master_data_catalogue_parent_catalogu",
                schema: "master_data_config",
                table: "master_data_catalogue");

            migrationBuilder.DropIndex(
                name: "ix_master_data_catalogue_parent_catalogue_id",
                schema: "master_data_config",
                table: "master_data_catalogue");

            migrationBuilder.DropColumn(
                name: "parent_catalogue_id",
                schema: "master_data_config",
                table: "master_data_catalogue");
        }
    }
}
