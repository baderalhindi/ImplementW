using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_DeleteProjectsAndUsersUserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_status",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_access_relationship_end_reason",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "project",
                table: "project",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_permission_override",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effect = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reason_lang = table.Column<string>(type: "char(2)", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_permission_override", x => x.id);
                    table.CheckConstraint("ck_user_permission_override_data_scope", "\"data_scope\" IN ('ALL', 'DEPT', 'OWN', 'ASSIGNED', 'ENTITY', 'READ_ONLY')");
                    table.CheckConstraint("ck_user_permission_override_effect", "\"effect\" IN ('GRANT', 'REMOVE')");
                    table.CheckConstraint("ck_user_permission_override_reason_lang", "\"reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_user_permission_override_scope", "(effect = 'GRANT') = (data_scope IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_user_permission_override_permission_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "identity_access",
                        principalTable: "permission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_permission_override_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_status",
                schema: "identity_access",
                table: "user",
                sql: "\"status\" IN ('ACTIVE', 'DISABLED', 'DELETED')");

            migrationBuilder.CreateIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project",
                column: "legacy_reference",
                unique: true,
                filter: "legacy_reference IS NOT NULL AND deleted_at IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_access_relationship_end_reason",
                schema: "identity_access",
                table: "access_relationship",
                sql: "\"end_reason\" IN ('PROJECT_CLOSED', 'ROLE_CHANGE', 'MIGRATED', 'MANUAL', 'EXPIRED', 'USER_DELETED')");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_override_permission_id",
                schema: "identity_access",
                table: "user_permission_override",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_permission_override_user_id_permission_id",
                schema: "identity_access",
                table: "user_permission_override",
                columns: new[] { "user_id", "permission_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The earlier schema has neither state: a deleted user goes back to disabled, an assignment ended by it to manually ended,
            // and a deleted project, losing its marker, comes back into the platform's reads.
            migrationBuilder.Sql("""
                ALTER TABLE identity_access."user" DROP CONSTRAINT ck_user_status;
                UPDATE identity_access."user" SET status = 'DISABLED' WHERE status = 'DELETED';
                ALTER TABLE identity_access."user" ADD CONSTRAINT ck_user_status CHECK ("status" IN ('ACTIVE', 'DISABLED', 'DELETED'));
                ALTER TABLE identity_access.access_relationship DROP CONSTRAINT ck_access_relationship_end_reason;
                UPDATE identity_access.access_relationship SET end_reason = 'MANUAL' WHERE end_reason = 'USER_DELETED';
                ALTER TABLE identity_access.access_relationship ADD CONSTRAINT ck_access_relationship_end_reason
                    CHECK ("end_reason" IN ('PROJECT_CLOSED', 'ROLE_CHANGE', 'MIGRATED', 'MANUAL', 'EXPIRED', 'USER_DELETED'));
                """);

            migrationBuilder.DropTable(
                name: "user_permission_override",
                schema: "identity_access");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_status",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_access_relationship_end_reason",
                schema: "identity_access",
                table: "access_relationship");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                schema: "project",
                table: "project");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_status",
                schema: "identity_access",
                table: "user",
                sql: "\"status\" IN ('ACTIVE', 'DISABLED')");

            migrationBuilder.CreateIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project",
                column: "legacy_reference",
                unique: true,
                filter: "legacy_reference IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_access_relationship_end_reason",
                schema: "identity_access",
                table: "access_relationship",
                sql: "\"end_reason\" IN ('PROJECT_CLOSED', 'ROLE_CHANGE', 'MIGRATED', 'MANUAL', 'EXPIRED')");
        }
    }
}
