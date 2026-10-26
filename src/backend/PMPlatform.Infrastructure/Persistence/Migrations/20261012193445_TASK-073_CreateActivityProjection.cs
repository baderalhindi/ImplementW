using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-073, 1 of 2: the attribute digest on <c>audit_event</c>, now part of its chained hash; the Activity projection
    /// <c>business_activity_entry</c> (I-48) with one line per event at most; and the projection's checkpoint. The two foreign keys
    /// that leave the schema are the next migration's (R-7).
    /// </summary>
    public partial class TASK073_CreateActivityProjection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "attribute_digest",
                schema: "audit_activity",
                table: "audit_event",
                type: "char(64)",
                nullable: true);

            // The chain covers each event's attributes through their digest (TASK-033 F-2). concat_ws skips a null argument, so an
            // event stored before this migration, whose digest is null, hashes exactly as it did: the existing chain stays valid.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_activity.audit_event_hash(e audit_activity.audit_event) RETURNS char(64)
                LANGUAGE sql STABLE AS $function$
                    SELECT encode(sha256(convert_to(concat_ws('|',
                        coalesce(e.previous_event_hash, ''),
                        e.id::text,
                        to_char(e.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        to_char(e.recorded_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        e.event_class,
                        e.event_type,
                        e.actor_type,
                        coalesce(e.actor_user_id::text, ''),
                        coalesce(e.subject_module, ''),
                        coalesce(e.subject_type, ''),
                        coalesce(e.subject_id::text, ''),
                        coalesce(e.scope_project_id::text, ''),
                        coalesce(e.scope_external_entity_id::text, ''),
                        e.correlation_id::text,
                        e.outcome,
                        coalesce(e.data_classification_item_id::text, ''),
                        e.created_by::text,
                        e.attribute_digest), 'UTF8')), 'hex');
                $function$;
                """);

            migrationBuilder.CreateTable(
                name: "activity_projection_checkpoint",
                schema: "audit_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_audit_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_projection_checkpoint", x => x.id);
                    table.CheckConstraint("ck_activity_projection_checkpoint_position", "(last_recorded_at IS NULL) = (last_audit_event_id IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "business_activity_entry",
                schema: "audit_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    activity_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    summary_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    summary_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_activity_entry", x => x.id);
                    table.ForeignKey(
                        name: "fk_business_activity_entry_audit_event_audit_event_id",
                        column: x => x.audit_event_id,
                        principalSchema: "audit_activity",
                        principalTable: "audit_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_activity_entry_audit_event_id",
                schema: "audit_activity",
                table: "business_activity_entry",
                column: "audit_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_business_activity_entry_project_id_occurred_at_id",
                schema: "audit_activity",
                table: "business_activity_entry",
                columns: new[] { "project_id", "occurred_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_projection_checkpoint",
                schema: "audit_activity");

            migrationBuilder.DropTable(
                name: "business_activity_entry",
                schema: "audit_activity");

            // The TASK-033 definition, before the column it would name is dropped.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_activity.audit_event_hash(e audit_activity.audit_event) RETURNS char(64)
                LANGUAGE sql STABLE AS $function$
                    SELECT encode(sha256(convert_to(concat_ws('|',
                        coalesce(e.previous_event_hash, ''),
                        e.id::text,
                        to_char(e.occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        to_char(e.recorded_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        e.event_class,
                        e.event_type,
                        e.actor_type,
                        coalesce(e.actor_user_id::text, ''),
                        coalesce(e.subject_module, ''),
                        coalesce(e.subject_type, ''),
                        coalesce(e.subject_id::text, ''),
                        coalesce(e.scope_project_id::text, ''),
                        coalesce(e.scope_external_entity_id::text, ''),
                        e.correlation_id::text,
                        e.outcome,
                        coalesce(e.data_classification_item_id::text, ''),
                        e.created_by::text), 'UTF8')), 'hex');
                $function$;
                """);

            migrationBuilder.DropColumn(
                name: "attribute_digest",
                schema: "audit_activity",
                table: "audit_event");
        }
    }
}
