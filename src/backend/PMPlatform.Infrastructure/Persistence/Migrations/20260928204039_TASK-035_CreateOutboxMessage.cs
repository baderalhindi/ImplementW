using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-035, 1 of 4: the transactional outbox <c>common.outbox_message</c> (ERD D-17; event-conventions EV-6), through
    /// which the approval outcome reaches its source module. Unique on (message_type, message_key), so a message cannot
    /// be published twice; the partial index is the dispatcher's queue of undispatched messages.
    /// </summary>
    public partial class TASK035_CreateOutboxMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_message",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    message_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    message_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_message", x => x.id);
                    table.CheckConstraint("ck_outbox_message_message_type", "\"message_type\" IN ('DOMAIN_EVENT', 'NOTIFICATION_INTENT', 'AUDIT_EVENT')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_message_type_message_key",
                schema: "common",
                table: "outbox_message",
                columns: new[] { "message_type", "message_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_message_occurred_at_id",
                schema: "common",
                table: "outbox_message",
                columns: new[] { "occurred_at", "id" },
                filter: "dispatched_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_message",
                schema: "common");
        }
    }
}
