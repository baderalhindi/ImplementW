using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK112_UserNationalIdAndDeletedDepartments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "national_id_fingerprint",
                schema: "identity_access",
                table: "user",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_id_last_digits",
                schema: "identity_access",
                table: "user",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "national_id_sealed",
                schema: "identity_access",
                table: "user",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "identity_access",
                table: "department",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_national_id_fingerprint",
                schema: "identity_access",
                table: "user",
                column: "national_id_fingerprint",
                unique: true,
                filter: "national_id_fingerprint IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_national_id_fingerprint",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "national_id_fingerprint",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "national_id_last_digits",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "national_id_sealed",
                schema: "identity_access",
                table: "user");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "identity_access",
                table: "department");
        }
    }
}
