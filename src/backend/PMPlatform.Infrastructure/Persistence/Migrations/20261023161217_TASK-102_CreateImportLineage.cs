using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK102_CreateImportLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "import_batch",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    created_count = table.Column<int>(type: "integer", nullable: false),
                    skipped_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_import_batch", x => x.id);
                    table.CheckConstraint("ck_import_batch_append_only", "updated_at = created_at AND updated_by = created_by");
                });

            migrationBuilder.CreateTable(
                name: "imported_record",
                schema: "common",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    register_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    legacy_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_imported_record", x => x.id);
                    table.CheckConstraint("ck_imported_record_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.ForeignKey(
                        name: "fk_imported_record_import_batch_import_batch_id",
                        column: x => x.import_batch_id,
                        principalSchema: "common",
                        principalTable: "import_batch",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_import_batch_register_code_created_at",
                schema: "common",
                table: "import_batch",
                columns: new[] { "register_code", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_imported_record_import_batch_id",
                schema: "common",
                table: "imported_record",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_imported_record_register_code_legacy_id",
                schema: "common",
                table: "imported_record",
                columns: new[] { "register_code", "legacy_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "imported_record",
                schema: "common");

            migrationBuilder.DropTable(
                name: "import_batch",
                schema: "common");
        }
    }
}
