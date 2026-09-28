using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-033, 1 of 2: the formal audit store (ERD §5.22) — <c>audit_event</c>, <c>audit_event_attribute</c> and
    /// <c>audit_forwarding_record</c>; <c>business_activity_entry</c> is TASK-073's. The database, not the application,
    /// places each event in the hash chain, and it refuses every UPDATE, DELETE and TRUNCATE of an event or attribute,
    /// whoever issues it. The foreign keys to other modules follow in 2 of 2.
    /// </summary>
    public partial class TASK033_CreateAuditActivityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_event",
                schema: "audit_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    subject_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_external_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    previous_event_hash = table.Column<string>(type: "char(64)", nullable: true),
                    event_hash = table.Column<string>(type: "char(64)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event", x => x.id);
                    table.CheckConstraint("ck_audit_event_actor_type", "\"actor_type\" IN ('USER', 'SERVICE', 'INTEGRATION')");
                    table.CheckConstraint("ck_audit_event_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_audit_event_event_class", "\"event_class\" IN ('AUTHENTICATION', 'AUTHORIZATION_DENIAL', 'PRIVILEGED_ACTION', 'PERMISSION_CHANGE', 'LIFECYCLE_TRANSITION', 'DATA_CHANGE', 'APPROVAL_DECISION', 'INTEGRATION', 'CONFIGURATION_CHANGE')");
                    table.CheckConstraint("ck_audit_event_outcome", "\"outcome\" IN ('SUCCESS', 'DENIED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "audit_event_attribute",
                schema: "audit_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    old_value = table.Column<string>(type: "text", nullable: true),
                    new_value = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_event_attribute", x => x.id);
                    table.CheckConstraint("ck_audit_event_attribute_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.ForeignKey(
                        name: "fk_audit_event_attribute_audit_event_audit_event_id",
                        column: x => x.audit_event_id,
                        principalSchema: "audit_activity",
                        principalTable: "audit_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_forwarding_record",
                schema: "audit_activity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    audit_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forwarded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    invocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_forwarding_record", x => x.id);
                    table.CheckConstraint("ck_audit_forwarding_record_status", "\"status\" IN ('PENDING', 'FORWARDED', 'FAILED')");
                    table.ForeignKey(
                        name: "fk_audit_forwarding_record_audit_event_audit_event_id",
                        column: x => x.audit_event_id,
                        principalSchema: "audit_activity",
                        principalTable: "audit_event",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_actor_user_id_occurred_at",
                schema: "audit_activity",
                table: "audit_event",
                columns: new[] { "actor_user_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_occurred_at_id",
                schema: "audit_activity",
                table: "audit_event",
                columns: new[] { "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_recorded_at_id",
                schema: "audit_activity",
                table: "audit_event",
                columns: new[] { "recorded_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_scope_project_id_occurred_at_id",
                schema: "audit_activity",
                table: "audit_event",
                columns: new[] { "scope_project_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_event_attribute_audit_event_id_attribute_name",
                schema: "audit_activity",
                table: "audit_event_attribute",
                columns: new[] { "audit_event_id", "attribute_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_forwarding_record_audit_event_id",
                schema: "audit_activity",
                table: "audit_forwarding_record",
                column: "audit_event_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_forwarding_record_status_created_at",
                schema: "audit_activity",
                table: "audit_forwarding_record",
                columns: new[] { "status", "created_at" });
                    // The hash of an event: SHA-256 over its previous event's hash and every column but the three the trigger sets
            // from it, each rendered as text and joined with '|'. An absent value is the empty string.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_activity.audit_event_hash(e audit_activity.audit_event) RETURNS char(64)
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

            // One writer at a time extends the chain: the transaction-scoped lock is held until the inserting transaction
            // commits, so the next insert sees this one. recorded_at strictly increases, so the chain order is
            // recorded_at, and the head is the row with the latest one.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit_activity.chain_audit_event() RETURNS trigger
                LANGUAGE plpgsql AS $function$
                DECLARE
                    head record;
                BEGIN
                    PERFORM pg_advisory_xact_lock(hashtextextended('audit_activity.audit_event', 0));
                    SELECT e.recorded_at, e.event_hash INTO head
                    FROM audit_activity.audit_event e
                    ORDER BY e.recorded_at DESC, e.id DESC
                    LIMIT 1;
                    NEW.recorded_at := greatest(clock_timestamp(), head.recorded_at + interval '1 microsecond');
                    NEW.previous_event_hash := head.event_hash;
                    NEW.event_hash := audit_activity.audit_event_hash(NEW);
                    RETURN NEW;
                END;
                $function$;
                """);

            migrationBuilder.Sql("""
                CREATE FUNCTION audit_activity.reject_audit_change() RETURNS trigger
                LANGUAGE plpgsql AS $function$
                BEGIN
                    RAISE EXCEPTION '%.% is append-only: % is not allowed', TG_TABLE_SCHEMA, TG_TABLE_NAME, TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END;
                $function$;
                """);

            migrationBuilder.Sql("CREATE TRIGGER chain_audit_event BEFORE INSERT ON audit_activity.audit_event FOR EACH ROW EXECUTE FUNCTION audit_activity.chain_audit_event();");
            migrationBuilder.Sql("CREATE TRIGGER append_only BEFORE UPDATE OR DELETE ON audit_activity.audit_event FOR EACH ROW EXECUTE FUNCTION audit_activity.reject_audit_change();");
            migrationBuilder.Sql("CREATE TRIGGER append_only_truncate BEFORE TRUNCATE ON audit_activity.audit_event FOR EACH STATEMENT EXECUTE FUNCTION audit_activity.reject_audit_change();");
            migrationBuilder.Sql("CREATE TRIGGER append_only BEFORE UPDATE OR DELETE ON audit_activity.audit_event_attribute FOR EACH ROW EXECUTE FUNCTION audit_activity.reject_audit_change();");
            migrationBuilder.Sql("CREATE TRIGGER append_only_truncate BEFORE TRUNCATE ON audit_activity.audit_event_attribute FOR EACH STATEMENT EXECUTE FUNCTION audit_activity.reject_audit_change();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER append_only_truncate ON audit_activity.audit_event_attribute;");
            migrationBuilder.Sql("DROP TRIGGER append_only ON audit_activity.audit_event_attribute;");
            migrationBuilder.Sql("DROP TRIGGER append_only_truncate ON audit_activity.audit_event;");
            migrationBuilder.Sql("DROP TRIGGER append_only ON audit_activity.audit_event;");
            migrationBuilder.Sql("DROP TRIGGER chain_audit_event ON audit_activity.audit_event;");
            migrationBuilder.Sql("DROP FUNCTION audit_activity.reject_audit_change();");
            migrationBuilder.Sql("DROP FUNCTION audit_activity.chain_audit_event();");
            migrationBuilder.Sql("DROP FUNCTION audit_activity.audit_event_hash(audit_activity.audit_event);");

            migrationBuilder.DropTable(
                name: "audit_event_attribute",
                schema: "audit_activity");

            migrationBuilder.DropTable(
                name: "audit_forwarding_record",
                schema: "audit_activity");

            migrationBuilder.DropTable(
                name: "audit_event",
                schema: "audit_activity");
        }
    }
}
