using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-026, 2 of 3 (<c>docs/architecture/indexing-strategy.md</c> I-09, I-10): a catalogue's items by state in
    /// display order, and a configuration family's versions by state and effective date for as-of resolution.
    /// </summary>
    public partial class TASK026_AddMasterDataConfigListIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_master_data_item_catalogue_id_lifecycle_state_sort_order_id",
                schema: "master_data_config",
                table: "master_data_item",
                columns: new[] { "catalogue_id", "lifecycle_state", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_configuration_version_family_state_effective_from",
                schema: "master_data_config",
                table: "configuration_version",
                columns: new[] { "configuration_family_id", "lifecycle_state", "effective_from" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_master_data_item_catalogue_id_lifecycle_state_sort_order_id",
                schema: "master_data_config",
                table: "master_data_item");

            migrationBuilder.DropIndex(
                name: "ix_configuration_version_family_state_effective_from",
                schema: "master_data_config",
                table: "configuration_version");
        }
    }
}
