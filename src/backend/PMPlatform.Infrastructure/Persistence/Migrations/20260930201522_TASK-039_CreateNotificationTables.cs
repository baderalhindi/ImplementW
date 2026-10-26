using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-039, 1 of 3: the WF-15 tables of ERD §5.18 <c>notifications</c> — <c>notification_template</c>,
    /// <c>notification_intent</c>, <c>notification_intent_parameter</c>, <c>notification_delivery</c>,
    /// <c>notification_preference</c> — with the foreign keys inside the schema, the register indexes I-46 and I-47, and the
    /// worker's queues. The foreign keys to other modules' tables follow in 2 of 3 (migrations README R-7).
    /// </summary>
    public partial class TASK039_CreateNotificationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "notification_intent",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_family_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    condition_revalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    suppression_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    deep_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_intent", x => x.id);
                    table.CheckConstraint("ck_notification_intent_reason", "(status IN ('SUPPRESSED', 'FAILED')) = (suppression_reason IS NOT NULL)");
                    table.CheckConstraint("ck_notification_intent_scheduled", "status <> 'SCHEDULED' OR scheduled_for IS NOT NULL");
                    table.CheckConstraint("ck_notification_intent_status", "\"status\" IN ('RECEIVED', 'SCHEDULED', 'RESOLVING', 'ROUTED', 'SUPPRESSED', 'COMPLETED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "notification_preference",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_family_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preference", x => x.id);
                    table.CheckConstraint("ck_notification_preference_channel", "\"channel\" IN ('IN_APP', 'EMAIL', 'SMS')");
                    table.CheckConstraint("ck_notification_preference_channel_choice", "channel IN ('EMAIL', 'SMS')");
                });

            migrationBuilder.CreateTable(
                name: "notification_template",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_family_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    subject_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    subject_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body_ar = table.Column<string>(type: "text", nullable: false),
                    body_en = table.Column<string>(type: "text", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_template", x => x.id);
                    table.CheckConstraint("ck_notification_template_channel", "\"channel\" IN ('IN_APP', 'EMAIL', 'SMS')");
                    table.CheckConstraint("ck_notification_template_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.CheckConstraint("ck_notification_template_subject", "(channel = 'SMS') = (subject_ar IS NULL) AND (subject_ar IS NULL) = (subject_en IS NULL)");
                    table.CheckConstraint("ck_notification_template_version_no", "version_no >= 1");
                });

            migrationBuilder.CreateTable(
                name: "notification_intent_parameter",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameter_value = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_intent_parameter", x => x.id);
                    table.CheckConstraint("ck_notification_intent_parameter_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.ForeignKey(
                        name: "fk_notification_intent_parameter_notification_intent_notificat",
                        column: x => x.notification_intent_id,
                        principalSchema: "notifications",
                        principalTable: "notification_intent",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_intent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    notification_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rendered_language = table.Column<string>(type: "char(2)", nullable: false),
                    rendered_subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rendered_body = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    suppression_reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dead_lettered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    segment_count = table.Column<short>(type: "smallint", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_delivery", x => x.id);
                    table.CheckConstraint("ck_notification_delivery_attempt_count", "attempt_count >= 0");
                    table.CheckConstraint("ck_notification_delivery_channel", "\"channel\" IN ('IN_APP', 'EMAIL', 'SMS')");
                    table.CheckConstraint("ck_notification_delivery_dead_letter", "(status = 'DEAD_LETTER') = (dead_lettered_at IS NOT NULL)");
                    table.CheckConstraint("ck_notification_delivery_read", "(status = 'READ') = (read_at IS NOT NULL) AND (read_at IS NULL OR channel = 'IN_APP')");
                    table.CheckConstraint("ck_notification_delivery_reason", "(status = 'SUPPRESSED') = (suppression_reason IS NOT NULL)");
                    table.CheckConstraint("ck_notification_delivery_rendered_language", "\"rendered_language\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_notification_delivery_segment_count", "(segment_count IS NULL OR segment_count >= 1) AND (segment_count IS NULL OR channel = 'SMS')");
                    table.CheckConstraint("ck_notification_delivery_status", "\"status\" IN ('PENDING', 'SENT', 'DELIVERED', 'FAILED', 'DEAD_LETTER', 'SUPPRESSED', 'READ')");
                    table.ForeignKey(
                        name: "fk_notification_delivery_notification_intent_notification_inte",
                        column: x => x.notification_intent_id,
                        principalSchema: "notifications",
                        principalTable: "notification_intent",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notification_delivery_notification_template_notification_te",
                        column: x => x.notification_template_id,
                        principalSchema: "notifications",
                        principalTable: "notification_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_intent_recipient_channel",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "notification_intent_id", "recipient_user_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_notification_template_id",
                schema: "notifications",
                table: "notification_delivery",
                column: "notification_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_recipient_user_id_channel_created_at_",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "recipient_user_id", "channel", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_recipient_user_id_channel_read_at",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "recipient_user_id", "channel", "read_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_send_queue",
                schema: "notifications",
                table: "notification_delivery",
                columns: new[] { "next_attempt_at", "id" },
                filter: "status IN ('PENDING', 'FAILED')");

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_received_at_id",
                schema: "notifications",
                table: "notification_intent",
                columns: new[] { "received_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_route_queue",
                schema: "notifications",
                table: "notification_intent",
                columns: new[] { "updated_at", "id" },
                filter: "status IN ('RECEIVED', 'SCHEDULED')");

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_routed",
                schema: "notifications",
                table: "notification_intent",
                column: "id",
                filter: "status = 'ROUTED'");

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_source_event_type_source_reference",
                schema: "notifications",
                table: "notification_intent",
                columns: new[] { "source_event_type", "source_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_intent_parameter_intent_key",
                schema: "notifications",
                table: "notification_intent_parameter",
                columns: new[] { "notification_intent_id", "parameter_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_preference_user_family_channel",
                schema: "notifications",
                table: "notification_preference",
                columns: new[] { "user_id", "event_family_code", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_event_type_channel_version_no",
                schema: "notifications",
                table: "notification_template",
                columns: new[] { "event_type", "channel", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_template_published",
                schema: "notifications",
                table: "notification_template",
                columns: new[] { "event_type", "channel" },
                filter: "lifecycle_state = 'PUBLISHED'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_delivery",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_intent_parameter",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_preference",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_template",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_intent",
                schema: "notifications");
        }
    }
}
