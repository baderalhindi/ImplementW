using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK075_AddIntegrationMonitoringCrossModuleForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_operational_alert_acknowledged_by_user_id",
                schema: "integration_monitoring",
                table: "operational_alert",
                column: "acknowledged_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_integration_definition_owner_role_id",
                schema: "integration_monitoring",
                table: "integration_definition",
                column: "owner_role_id");

            migrationBuilder.AddForeignKey(
                name: "fk_integration_definition_role_owner_role_id",
                schema: "integration_monitoring",
                table: "integration_definition",
                column: "owner_role_id",
                principalSchema: "identity_access",
                principalTable: "role",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_operational_alert_user_acknowledged_by_user_id",
                schema: "integration_monitoring",
                table: "operational_alert",
                column: "acknowledged_by_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_integration_definition_role_owner_role_id",
                schema: "integration_monitoring",
                table: "integration_definition");

            migrationBuilder.DropForeignKey(
                name: "fk_operational_alert_user_acknowledged_by_user_id",
                schema: "integration_monitoring",
                table: "operational_alert");

            migrationBuilder.DropIndex(
                name: "ix_operational_alert_acknowledged_by_user_id",
                schema: "integration_monitoring",
                table: "operational_alert");

            migrationBuilder.DropIndex(
                name: "ix_integration_definition_owner_role_id",
                schema: "integration_monitoring",
                table: "integration_definition");
        }
    }
}
