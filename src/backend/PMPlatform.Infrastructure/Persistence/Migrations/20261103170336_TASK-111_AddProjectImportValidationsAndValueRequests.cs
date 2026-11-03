using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK111_AddProjectImportValidationsAndValueRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_import_validation",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_content = table.Column<byte[]>(type: "bytea", nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_import_validation", x => x.id);
                    table.CheckConstraint("ck_project_import_validation_result", "(status = 'COMPLETED') = (result IS NOT NULL) AND (status = 'FAILED') = (failure_code IS NOT NULL)");
                    table.CheckConstraint("ck_project_import_validation_status", "\"status\" IN ('QUEUED', 'RUNNING', 'COMPLETED', 'FAILED')");
                });

            migrationBuilder.CreateTable(
                name: "project_import_value_request",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    resolution_note_lang = table.Column<string>(type: "char(2)", nullable: true),
                    resolution_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_import_value_request", x => x.id);
                    table.CheckConstraint("ck_project_import_value_request_resolution", "(status = 'OPEN') = (resolved_at IS NULL) AND (status = 'OPEN') = (resolved_by_user_id IS NULL)");
                    table.CheckConstraint("ck_project_import_value_request_resolution_note_lang", "\"resolution_note_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_import_value_request_resolution_note_pair", "(\"resolution_note\" IS NULL) = (\"resolution_note_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_import_value_request_status", "\"status\" IN ('OPEN', 'ADDED', 'DECLINED')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_import_validation_status_created_at_id",
                schema: "project",
                table: "project_import_validation",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_import_value_request_status_created_at_id",
                schema: "project",
                table: "project_import_value_request",
                columns: new[] { "status", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "project_import_validation",
                schema: "project");

            migrationBuilder.DropTable(
                name: "project_import_value_request",
                schema: "project");
        }
    }
}
