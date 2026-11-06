using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddPlaceCoordinates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                schema: "master_data_config",
                table: "master_data_item",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                schema: "master_data_config",
                table: "master_data_item",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_master_data_item_coordinates",
                schema: "master_data_config",
                table: "master_data_item",
                sql: "(latitude IS NULL) = (longitude IS NULL) AND (latitude IS NULL OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_master_data_item_coordinates",
                schema: "master_data_config",
                table: "master_data_item");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "master_data_config",
                table: "master_data_item");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "master_data_config",
                table: "master_data_item");
        }
    }
}
