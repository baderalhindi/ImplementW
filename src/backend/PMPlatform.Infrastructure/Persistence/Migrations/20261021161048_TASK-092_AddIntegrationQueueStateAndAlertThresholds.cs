using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK092_AddIntegrationQueueStateAndAlertThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replaced below by a CHECK that accepts every value the previous one did, and QUEUE_BACKLOG: not a contraction.
            migrationBuilder.Sql("ALTER TABLE integration_monitoring.operational_alert DROP CONSTRAINT ck_operational_alert_alert_type; -- EXPAND-THEN-CONTRACT-REVIEWED: widened, re-added in this migration (TASK-092)");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "availability_changed_at",
                schema: "integration_monitoring",
                table: "integration_instance",
                type: "timestamp with time zone",
                nullable: true);

            // An instance already DOWN or UP has held that availability since its latest technical failure or success: the outcome
            // that set it (IntegrationHealth.AvailabilityOf). UNKNOWN has never changed.
            migrationBuilder.Sql("""
                UPDATE integration_monitoring.integration_instance
                SET availability_changed_at = CASE availability WHEN 'DOWN' THEN last_failure_at WHEN 'UP' THEN last_success_at END
                WHERE availability <> 'UNKNOWN';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_alert_alert_type",
                schema: "integration_monitoring",
                table: "operational_alert",
                sql: "\"alert_type\" IN ('AVAILABILITY', 'FRESHNESS', 'RECONCILIATION_MISMATCH', 'DEAD_LETTER', 'DELIVERY_RATE', 'QUEUE_BACKLOG')");

            migrationBuilder.CreateIndex(
                name: "ix_invocation_queue_state",
                schema: "integration_monitoring",
                table: "invocation",
                columns: new[] { "integration_instance_id", "status" },
                filter: "status IN ('IN_PROGRESS', 'RETRY_SCHEDULED', 'DEAD_LETTER')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_operational_alert_alert_type",
                schema: "integration_monitoring",
                table: "operational_alert");

            migrationBuilder.DropIndex(
                name: "ix_invocation_queue_state",
                schema: "integration_monitoring",
                table: "invocation");

            migrationBuilder.DropColumn(
                name: "availability_changed_at",
                schema: "integration_monitoring",
                table: "integration_instance");

            migrationBuilder.AddCheckConstraint(
                name: "ck_operational_alert_alert_type",
                schema: "integration_monitoring",
                table: "operational_alert",
                sql: "\"alert_type\" IN ('AVAILABILITY', 'FRESHNESS', 'RECONCILIATION_MISMATCH', 'DEAD_LETTER', 'DELIVERY_RATE')");
        }
    }
}
