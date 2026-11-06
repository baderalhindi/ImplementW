using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_AddLocalAccountsAndRoleSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "account_ends_on",
                schema: "identity_access",
                table: "user",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "account_source",
                schema: "identity_access",
                table: "user",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "DIRECTORY");

            migrationBuilder.AddColumn<int>(
                name: "failed_sign_in_count",
                schema: "identity_access",
                table: "user",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_until",
                schema: "identity_access",
                table: "user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "must_change_password",
                schema: "identity_access",
                table: "user",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                schema: "identity_access",
                table: "user",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "allowed_scopes",
                schema: "identity_access",
                table: "role",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY['ALL']::text[]");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                schema: "identity_access",
                table: "role",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "landing_dashboard_code",
                schema: "identity_access",
                table: "role",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "user_password_history",
                schema: "identity_access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_password_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_password_history_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity_access",
                        principalTable: "user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_account_source",
                schema: "identity_access",
                table: "user",
                sql: "\"account_source\" IN ('DIRECTORY', 'LOCAL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_local_password",
                schema: "identity_access",
                table: "user",
                sql: "account_source <> 'LOCAL' OR password_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_password_history_user_id_created_at",
                schema: "identity_access",
                table: "user_password_history",
                columns: new[] { "user_id", "created_at" });

            // The canonical roles' settings, as the seed sets them for a new database (the 5 Nov 2026 review §6.2).
            migrationBuilder.Sql("""
                UPDATE identity_access.role r SET allowed_scopes = v.scopes::text[], landing_dashboard_code = v.landing
                FROM (VALUES
                    ('R01', '{ALL,DEPARTMENT,PROJECT}', 'GOVERNANCE'), ('R02', '{ALL,DEPARTMENT,PROJECT}', 'PORTFOLIO'), ('R03', '{DEPARTMENT}', 'PORTFOLIO'),
                    ('R04', '{ALL,DEPARTMENT,PROJECT,ENTITY}', 'GOVERNANCE'), ('R05', '{PROJECT}', 'GOVERNANCE'), ('R06', '{DEPARTMENT,PROJECT}', 'PORTFOLIO'),
                    ('R07', '{ALL,DEPARTMENT,PROJECT}', 'PORTFOLIO'), ('R08', '{ALL,ENTITY,PROJECT}', 'PROJECT')) AS v (code, scopes, landing)
                WHERE r.code = v.code;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_password_history",
                schema: "identity_access");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_account_source",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_local_password",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "account_ends_on",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "account_source",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "failed_sign_in_count",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "locked_until",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "must_change_password",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "password_hash",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "allowed_scopes",
                schema: "identity_access",
                table: "role");

            migrationBuilder.DropColumn(
                name: "is_active",
                schema: "identity_access",
                table: "role");

            migrationBuilder.DropColumn(
                name: "landing_dashboard_code",
                schema: "identity_access",
                table: "role");
        }
    }
}
