using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK075_CreateIntegrationMonitoringTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_definition",
                schema: "integration_monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    integration_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    owner_role_id = table.Column<Guid>(type: "uuid", nullable: true),
                    max_attempts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)3),
                    retry_backoff_seconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 60),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_definition", x => x.id);
                    table.CheckConstraint("ck_integration_definition_direction", "\"direction\" IN ('INBOUND', 'OUTBOUND', 'BIDIRECTIONAL')");
                    table.CheckConstraint("ck_integration_definition_integration_kind", "\"integration_kind\" IN ('IDENTITY', 'MESSAGING', 'VERIFICATION', 'FINANCE', 'SECURITY', 'DATA')");
                    table.CheckConstraint("ck_integration_definition_max_attempts", "max_attempts >= 1");
                    table.CheckConstraint("ck_integration_definition_retry_backoff_seconds", "retry_backoff_seconds >= 0");
                    table.CheckConstraint("ck_integration_definition_status", "\"status\" IN ('ACTIVE', 'DISABLED', 'RETIRED')");
                });

            migrationBuilder.CreateTable(
                name: "integration_instance",
                schema: "integration_monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    endpoint_configuration_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    availability = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    freshness = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_success_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_failure_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_integration_instance", x => x.id);
                    table.CheckConstraint("ck_integration_instance_availability", "\"availability\" IN ('UP', 'DOWN', 'UNKNOWN')");
                    table.CheckConstraint("ck_integration_instance_freshness", "\"freshness\" IN ('FRESH', 'STALE', 'UNKNOWN')");
                    table.ForeignKey(
                        name: "fk_integration_instance_integration_definition_integration_def",
                        column: x => x.integration_definition_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "integration_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_run",
                schema: "integration_monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    run_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    records_read = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    records_applied = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    records_rejected = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    mismatch_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_run", x => x.id);
                    table.CheckConstraint("ck_sync_run_completed", "(status = 'RUNNING') = (completed_at IS NULL)");
                    table.CheckConstraint("ck_sync_run_counts", "records_read >= 0 AND records_applied >= 0 AND records_rejected >= 0 AND mismatch_count >= 0");
                    table.CheckConstraint("ck_sync_run_run_type", "\"run_type\" IN ('DIRECTORY_SYNC', 'RECONCILIATION')");
                    table.CheckConstraint("ck_sync_run_status", "\"status\" IN ('RUNNING', 'SUCCEEDED', 'PARTIAL', 'FAILED')");
                    table.ForeignKey(
                        name: "fk_sync_run_integration_instance_integration_instance_id",
                        column: x => x.integration_instance_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "integration_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "invocation",
                schema: "integration_monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sync_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_no = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    failure_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    response_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    error_summary = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invocation", x => x.id);
                    table.CheckConstraint("ck_invocation_attempt_no", "attempt_no >= 1");
                    table.CheckConstraint("ck_invocation_completed", "(status = 'IN_PROGRESS') = (completed_at IS NULL)");
                    table.CheckConstraint("ck_invocation_failure", "(status IN ('IN_PROGRESS', 'SUCCEEDED')) = (failure_category IS NULL)");
                    table.CheckConstraint("ck_invocation_failure_category", "\"failure_category\" IN ('TECHNICAL', 'BUSINESS_VALIDATION', 'SECURITY')");
                    table.CheckConstraint("ck_invocation_next_attempt", "(status = 'RETRY_SCHEDULED') = (next_attempt_at IS NOT NULL)");
                    table.CheckConstraint("ck_invocation_status", "\"status\" IN ('IN_PROGRESS', 'SUCCEEDED', 'FAILED', 'RETRY_SCHEDULED', 'DEAD_LETTER')");
                    table.ForeignKey(
                        name: "fk_invocation_integration_instance_integration_instance_id",
                        column: x => x.integration_instance_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "integration_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_invocation_sync_run_sync_run_id",
                        column: x => x.sync_run_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "sync_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operational_alert",
                schema: "integration_monitoring",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    integration_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sync_run_id = table.Column<Guid>(type: "uuid", nullable: true),
                    invocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    detail = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    resolution_note_lang = table.Column<string>(type: "char(2)", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operational_alert", x => x.id);
                    table.CheckConstraint("ck_operational_alert_acknowledged", "(acknowledged_by_user_id IS NULL) = (acknowledged_at IS NULL) AND (status <> 'ACKNOWLEDGED' OR acknowledged_at IS NOT NULL)");
                    table.CheckConstraint("ck_operational_alert_alert_type", "\"alert_type\" IN ('AVAILABILITY', 'FRESHNESS', 'RECONCILIATION_MISMATCH', 'DEAD_LETTER', 'DELIVERY_RATE')");
                    table.CheckConstraint("ck_operational_alert_resolution_note_lang", "\"resolution_note_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_operational_alert_resolution_note_pair", "(\"resolution_note\" IS NULL) = (\"resolution_note_lang\" IS NULL)");
                    table.CheckConstraint("ck_operational_alert_resolved", "(status IN ('RESOLVED', 'DISMISSED')) = (resolved_at IS NOT NULL) AND (status IN ('RESOLVED', 'DISMISSED')) = (resolution_note IS NOT NULL)");
                    table.CheckConstraint("ck_operational_alert_severity", "\"severity\" IN ('INFO', 'WARNING', 'CRITICAL')");
                    table.CheckConstraint("ck_operational_alert_status", "\"status\" IN ('OPEN', 'ACKNOWLEDGED', 'RESOLVED', 'DISMISSED')");
                    table.ForeignKey(
                        name: "fk_operational_alert_integration_instance_integration_instance",
                        column: x => x.integration_instance_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "integration_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_alert_invocation_invocation_id",
                        column: x => x.invocation_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "invocation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_operational_alert_sync_run_sync_run_id",
                        column: x => x.sync_run_id,
                        principalSchema: "integration_monitoring",
                        principalTable: "sync_run",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_integration_definition_code",
                schema: "integration_monitoring",
                table: "integration_definition",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_integration_instance_integration_definition_id_code",
                schema: "integration_monitoring",
                table: "integration_instance",
                columns: new[] { "integration_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invocation_correlation_id",
                schema: "integration_monitoring",
                table: "invocation",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_invocation_integration_instance_id_started_at_id",
                schema: "integration_monitoring",
                table: "invocation",
                columns: new[] { "integration_instance_id", "started_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_invocation_lineage_attempt",
                schema: "integration_monitoring",
                table: "invocation",
                columns: new[] { "integration_instance_id", "operation_code", "idempotency_key", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invocation_retry_queue",
                schema: "integration_monitoring",
                table: "invocation",
                columns: new[] { "next_attempt_at", "id" },
                filter: "status = 'RETRY_SCHEDULED'");

            migrationBuilder.CreateIndex(
                name: "ix_invocation_sync_run_id",
                schema: "integration_monitoring",
                table: "invocation",
                column: "sync_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_operational_alert_invocation_id",
                schema: "integration_monitoring",
                table: "operational_alert",
                column: "invocation_id");

            migrationBuilder.CreateIndex(
                name: "ix_operational_alert_live",
                schema: "integration_monitoring",
                table: "operational_alert",
                columns: new[] { "integration_instance_id", "alert_type" },
                unique: true,
                filter: "status IN ('OPEN', 'ACKNOWLEDGED')");

            migrationBuilder.CreateIndex(
                name: "ix_operational_alert_status_raised_at_id",
                schema: "integration_monitoring",
                table: "operational_alert",
                columns: new[] { "status", "raised_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_operational_alert_sync_run_id",
                schema: "integration_monitoring",
                table: "operational_alert",
                column: "sync_run_id");

            migrationBuilder.CreateIndex(
                name: "ix_sync_run_integration_instance_id_started_at_id",
                schema: "integration_monitoring",
                table: "sync_run",
                columns: new[] { "integration_instance_id", "started_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operational_alert",
                schema: "integration_monitoring");

            migrationBuilder.DropTable(
                name: "invocation",
                schema: "integration_monitoring");

            migrationBuilder.DropTable(
                name: "sync_run",
                schema: "integration_monitoring");

            migrationBuilder.DropTable(
                name: "integration_instance",
                schema: "integration_monitoring");

            migrationBuilder.DropTable(
                name: "integration_definition",
                schema: "integration_monitoring");
        }
    }
}
