using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_DeleteEntitiesAndMasterData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 2026-11-14: the administration deletes entities and master data values; the append-only trail keeps their ids, unconstrained, as for projects (R4-5).
            migrationBuilder.Sql("""
                ALTER TABLE audit_activity.audit_event DROP CONSTRAINT fk_audit_event_external_entity_scope_external_entity_id; -- EXPAND-THEN-CONTRACT-REVIEWED: an append-only trail must not block an entity's deletion
                ALTER TABLE audit_activity.audit_event DROP CONSTRAINT fk_audit_event_master_data_item_data_classification_item_id; -- EXPAND-THEN-CONTRACT-REVIEWED: an append-only trail must not block a master data value's deletion
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "fk_audit_event_external_entity_scope_external_entity_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "scope_external_entity_id",
                principalSchema: "identity_access",
                principalTable: "external_entity",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_event_master_data_item_data_classification_item_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "data_classification_item_id",
                principalSchema: "master_data_config",
                principalTable: "master_data_item",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
