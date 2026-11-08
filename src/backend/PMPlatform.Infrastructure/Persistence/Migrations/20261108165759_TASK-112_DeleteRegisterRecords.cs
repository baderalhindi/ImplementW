using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_DeleteRegisterRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Client review, item 8: each history guard lets a deletion through (deleted_at and deleted_by set, nothing else but the touch
            // columns changed) and holds a deleted row still. The block is inserted at the top of each guard and named, so Down removes it.
            migrationBuilder.Sql(GuardSql(remove: false));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "risk",
                table: "risk",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "risk",
                table: "risk",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "project_task",
                table: "project_task",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "project_task",
                table: "project_task",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "schedule",
                table: "project_milestone",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "schedule",
                table: "project_milestone",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "management_concern",
                table: "management_concern",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "management_concern",
                table: "management_concern",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "financial_kpi",
                table: "kpi_assignment",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "financial_kpi",
                table: "kpi_assignment",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "document_management",
                table: "document",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "document_management",
                table: "document",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "change_request",
                table: "change_request",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "change_request",
                table: "change_request",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(GuardSql(remove: true));

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "risk",
                table: "risk");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "project_task",
                table: "project_task");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "schedule",
                table: "project_milestone");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "schedule",
                table: "project_milestone");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "management_concern",
                table: "management_concern");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "financial_kpi",
                table: "kpi_assignment");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "document_management",
                table: "document");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "change_request",
                table: "change_request");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "change_request",
                table: "change_request");
        }

        private const string GuardMarker = "-- client review, item 8: deletion";

        private static readonly string[] Guards =
        [
            "change_request.guard_change_request", "document_management.guard_document", "financial_kpi.guard_kpi_assignment",
            "management_concern.guard_management_concern", "schedule.guard_project_milestone", "project_task.guard_project_task", "risk.guard_risk",
        ];

        /// <summary>Rewrites each guard's body: with the deletion block at its top, or without it.</summary>
        private static string GuardSql(bool remove)
        {
            const string block = GuardMarker + """

                IF TG_OP = 'UPDATE' THEN
                    IF OLD.deleted_at IS NOT NULL THEN
                        RAISE EXCEPTION '% % is deleted and changes no more', TG_TABLE_NAME, OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;
                    IF NEW.deleted_at IS NOT NULL
                       AND (to_jsonb(NEW) - ARRAY['deleted_at', 'deleted_by', 'updated_at', 'updated_by'])
                           = (to_jsonb(OLD) - ARRAY['deleted_at', 'deleted_by', 'updated_at', 'updated_by']) THEN
                        RETURN NEW;
                    END IF;
                END IF;
                -- end of client review, item 8
                """;
            string list = string.Join(", ", Guards.Select(g => $"'{g}'"));
            string rewrite = remove
                ? "regexp_replace(def, '" + GuardMarker + ".*?-- end of client review, item 8\n', '', 's')"
                : "regexp_replace(def, '\nBEGIN\n', chr(10) || 'BEGIN' || chr(10) || " + Quote(block) + " || chr(10))";
            return $$"""
                DO $migration$
                DECLARE f text; def text;
                BEGIN
                    FOREACH f IN ARRAY ARRAY[{{list}}] LOOP
                        def := pg_get_functiondef(f::regproc);
                        IF (position('{{GuardMarker}}' in def) > 0) = {{(remove ? "true" : "false")}} THEN
                            EXECUTE {{rewrite}};
                        END IF;
                    END LOOP;
                END
                $migration$;
                """;
        }

        private static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
    }
}
