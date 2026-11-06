using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddRound4ProjectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // R4-5: the audit trail outlives a project removed by the demo reset; its scope column stays, unconstrained.
            migrationBuilder.Sql("""
                ALTER TABLE audit_activity.audit_event DROP CONSTRAINT fk_audit_event_project_scope_project_id; -- EXPAND-THEN-CONTRACT-REVIEWED: an append-only trail must not block a project's removal
                ALTER TABLE project.project DROP CONSTRAINT ck_project_declared_source; -- EXPAND-THEN-CONTRACT-REVIEWED: widened to ENTITY_UPDATE, re-added below
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "entity_contact_user_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_strategic",
                schema: "project",
                table: "project",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "seed_source",
                schema: "project",
                table: "project",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title_en",
                schema: "project",
                table: "project",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_entity_contact_user_id",
                schema: "project",
                table: "project",
                column: "entity_contact_user_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project",
                sql: "\"declared_source\" IN ('IMPORT', 'MANUAL', 'ENTITY_UPDATE')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_seed_source",
                schema: "project",
                table: "project",
                sql: "seed_source IS NULL OR seed_source = 'DEMO'");

            migrationBuilder.AddForeignKey(
                name: "fk_project_user_entity_contact_user_id",
                schema: "project",
                table: "project",
                column: "entity_contact_user_id",
                principalSchema: "identity_access",
                principalTable: "user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The rows a seed or fixture made (the seed actor) are demonstration data: real data is never created by that actor.
            migrationBuilder.Sql("""
                ALTER TABLE project.project DISABLE TRIGGER USER;
                UPDATE project.project SET seed_source = 'DEMO' WHERE created_by = '00000000-0000-4000-8000-0000000000ff';
                ALTER TABLE project.project ENABLE TRIGGER USER;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_project_user_entity_contact_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_entity_contact_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_seed_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "entity_contact_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "is_strategic",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "seed_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "title_en",
                schema: "project",
                table: "project");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project",
                sql: "\"declared_source\" IN ('IMPORT', 'MANUAL')");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_event_project_scope_project_id",
                schema: "audit_activity",
                table: "audit_event",
                column: "scope_project_id",
                principalSchema: "project",
                principalTable: "project",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
