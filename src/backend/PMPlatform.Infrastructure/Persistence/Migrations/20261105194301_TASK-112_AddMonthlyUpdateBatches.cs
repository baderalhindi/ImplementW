using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddMonthlyUpdateBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "last_reminded_on",
                schema: "external_participation",
                table: "external_update_request",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "opened_at",
                schema: "external_participation",
                table: "external_update_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "update_request_batch_id",
                schema: "external_participation",
                table: "external_update_request",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "update_request_batch",
                schema: "external_participation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    series_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    repeat_monthly = table.Column<bool>(type: "boolean", nullable: false),
                    repeat_day = table.Column<int>(type: "integer", nullable: true),
                    next_run_on = table.Column<DateOnly>(type: "date", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    instructions_lang = table.Column<string>(type: "char(2)", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_update_request_batch", x => x.id);
                    table.CheckConstraint("ck_update_request_batch_instructions_lang", "\"instructions_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_update_request_batch_repeat", "repeat_monthly = (repeat_day IS NOT NULL)");
                    table.CheckConstraint("ck_update_request_batch_repeat_day", "repeat_day IS NULL OR repeat_day BETWEEN 1 AND 28");
                    table.ForeignKey(
                        name: "fk_update_request_batch_user_reviewer_user_id",
                        column: x => x.reviewer_user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_update_request_batch_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "update_request_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_update_request_batch_next_run_on",
                schema: "external_participation",
                table: "update_request_batch",
                column: "next_run_on",
                filter: "next_run_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_update_request_batch_reviewer_user_id",
                schema: "external_participation",
                table: "update_request_batch",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_update_request_batch_updated_at_id",
                schema: "external_participation",
                table: "update_request_batch",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.AddForeignKey(
                name: "fk_external_update_request_update_request_batch_update_request",
                schema: "external_participation",
                table: "external_update_request",
                column: "update_request_batch_id",
                principalSchema: "external_participation",
                principalTable: "update_request_batch",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // R4-2: a monthly update may also go to, and be answered by, an internal user holding the grant, such as an internal project manager. Every other schema stays external-only.
            migrationBuilder.Sql("""
                DO $do$
                DECLARE src text;
                BEGIN
                SELECT pg_get_functiondef('external_participation.guard_external_update_request'::regproc) INTO src;
                EXECUTE replace(src, 'NOT external_participation.is_external_user_of(NEW.responsible_user_id, NEW.external_entity_id)', 'NOT (external_participation.is_external_user_of(NEW.responsible_user_id, NEW.external_entity_id) OR (NEW.contribution_schema_code = ''MONTHLY_UPDATE'' AND external_participation.is_internal_user(NEW.responsible_user_id)))');
                SELECT pg_get_functiondef('external_participation.guard_external_contribution'::regproc) INTO src;
                EXECUTE replace(src, 'NOT external_participation.is_external_user_of(NEW.contributor_user_id, NEW.external_entity_id)', 'NOT (external_participation.is_external_user_of(NEW.contributor_user_id, NEW.external_entity_id) OR (external_participation.is_internal_user(NEW.contributor_user_id) AND EXISTS (SELECT 1 FROM external_participation.external_update_request r WHERE r.id = NEW.external_update_request_id AND r.contribution_schema_code = ''MONTHLY_UPDATE'')))');
                SELECT pg_get_functiondef('external_participation.guard_external_update_request'::regproc) INTO src;
                EXECUTE replace(src, 'IF NEW.status <> ''DRAFT'' OR NEW.origin <> ''AHDA_ISSUED'' THEN', 'IF (NEW.status <> ''DRAFT'' AND NEW.update_request_batch_id IS NULL) OR NEW.origin <> ''AHDA_ISSUED'' THEN');
                END $do$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $do$
                DECLARE src text;
                BEGIN
                SELECT pg_get_functiondef('external_participation.guard_external_update_request'::regproc) INTO src;
                EXECUTE replace(src, 'NOT (external_participation.is_external_user_of(NEW.responsible_user_id, NEW.external_entity_id) OR (NEW.contribution_schema_code = ''MONTHLY_UPDATE'' AND external_participation.is_internal_user(NEW.responsible_user_id)))', 'NOT external_participation.is_external_user_of(NEW.responsible_user_id, NEW.external_entity_id)');
                SELECT pg_get_functiondef('external_participation.guard_external_contribution'::regproc) INTO src;
                EXECUTE replace(src, 'NOT (external_participation.is_external_user_of(NEW.contributor_user_id, NEW.external_entity_id) OR (external_participation.is_internal_user(NEW.contributor_user_id) AND EXISTS (SELECT 1 FROM external_participation.external_update_request r WHERE r.id = NEW.external_update_request_id AND r.contribution_schema_code = ''MONTHLY_UPDATE'')))', 'NOT external_participation.is_external_user_of(NEW.contributor_user_id, NEW.external_entity_id)');
                SELECT pg_get_functiondef('external_participation.guard_external_update_request'::regproc) INTO src;
                EXECUTE replace(src, 'IF (NEW.status <> ''DRAFT'' AND NEW.update_request_batch_id IS NULL) OR NEW.origin <> ''AHDA_ISSUED'' THEN', 'IF NEW.status <> ''DRAFT'' OR NEW.origin <> ''AHDA_ISSUED'' THEN');
                END $do$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_external_update_request_update_request_batch_update_request",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropTable(
                name: "update_request_batch",
                schema: "external_participation");

            migrationBuilder.DropIndex(
                name: "ix_external_update_request_update_request_batch_id",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropColumn(
                name: "last_reminded_on",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropColumn(
                name: "opened_at",
                schema: "external_participation",
                table: "external_update_request");

            migrationBuilder.DropColumn(
                name: "update_request_batch_id",
                schema: "external_participation",
                table: "external_update_request");
        }
    }
}
