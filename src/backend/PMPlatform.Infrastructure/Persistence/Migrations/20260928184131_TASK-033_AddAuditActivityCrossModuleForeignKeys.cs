using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-033, 2 of 2: the four <c>audit_event</c> foreign keys that reference other modules' referenceable tables (ERD
    /// D-14) — actor to <c>user</c>, scope to <c>project</c> and <c>external_entity</c>, classification to
    /// <c>master_data_item</c> — with the indexes of the two that no register index already leads with.
    /// <c>audit_forwarding_record.invocation_id</c> gets none: <c>integration_monitoring.invocation</c> does not exist yet.
    /// </summary>
    public partial class TASK033_AddAuditActivityCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_audit_event_data_classification_item_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "data_classification_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_scope_external_entity_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "scope_external_entity_id");

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

            migrationBuilder.AddForeignKey(
                name: "fk_audit_event_project_scope_project_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "scope_project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_audit_event_user_actor_user_id",
                schema: "audit_activity",
                table: "audit_event",
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
                name: "fk_audit_event_external_entity_scope_external_entity_id",
                schema: "audit_activity",
                table: "audit_event");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_event_master_data_item_data_classification_item_id",
                schema: "audit_activity",
                table: "audit_event");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_event_project_scope_project_id",
                schema: "audit_activity",
                table: "audit_event");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_event_user_actor_user_id",
                schema: "audit_activity",
                table: "audit_event");

            migrationBuilder.DropIndex(
                name: "ix_audit_event_data_classification_item_id",
                schema: "audit_activity",
                table: "audit_event");

            migrationBuilder.DropIndex(
                name: "ix_audit_event_scope_external_entity_id",
                schema: "audit_activity",
                table: "audit_event");
        }
    }
}
