using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-037, 1 of 3: the WF-12 tables of ERD §5 <c>document_management</c> — <c>document</c>, <c>document_version</c>,
    /// <c>business_link</c>, <c>evidence_reference</c> — with the foreign keys inside the schema, the register indexes I-31
    /// and I-32, the scan worker's queue and the link-target lookup. The foreign keys to other modules' tables follow in 2 of
    /// 3 (migrations README R-7).
    /// </summary>
    public partial class TASK037_CreateDocumentManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document",
                schema: "document_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    description_lang = table.Column<string>(type: "char(2)", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document", x => x.id);
                    table.CheckConstraint("ck_document_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_document_description_pair", "(\"description\" IS NULL) = (\"description_lang\" IS NULL)");
                    table.CheckConstraint("ck_document_status", "\"status\" IN ('ACTIVE', 'ARCHIVED')");
                    table.CheckConstraint("ck_document_title_lang", "\"title_lang\" IN ('ar', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "business_link",
                schema: "document_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    unlinked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    unlinked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_link", x => x.id);
                    table.CheckConstraint("ck_business_link_link_role", "\"link_role\" IN ('ATTACHMENT', 'REFERENCE')");
                    table.CheckConstraint("ck_business_link_unlinked", "(unlinked_at IS NULL) = (unlinked_by_user_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_business_link_document_document_id",
                        column: x => x.document_id,
                        principalSchema: "document_management",
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "document_version",
                schema: "document_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    storage_object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scan_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    scan_completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    scan_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_version", x => x.id);
                    table.CheckConstraint("ck_document_version_checksum_sha256", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_document_version_scan_completed", "(scan_state = 'SCAN_PENDING') = (scan_completed_at IS NULL)");
                    table.CheckConstraint("ck_document_version_scan_state", "\"scan_state\" IN ('SCAN_PENDING', 'CLEAN', 'QUARANTINED', 'SCAN_FAILED')");
                    table.CheckConstraint("ck_document_version_size_bytes", "size_bytes >= 0");
                    table.CheckConstraint("ck_document_version_version_no", "version_no >= 1");
                    table.ForeignKey(
                        name: "fk_document_version_document_document_id",
                        column: x => x.document_id,
                        principalSchema: "document_management",
                        principalTable: "document",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence_reference",
                schema: "document_management",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_link_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    evidence_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    designated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    designated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence_reference", x => x.id);
                    table.CheckConstraint("ck_evidence_reference_status", "\"status\" IN ('VALID', 'WITHDRAWN')");
                    table.ForeignKey(
                        name: "fk_evidence_reference_business_link_business_link_id",
                        column: x => x.business_link_id,
                        principalSchema: "document_management",
                        principalTable: "business_link",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_reference_document_version_document_version_id",
                        column: x => x.document_version_id,
                        principalSchema: "document_management",
                        principalTable: "document_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_link_document_target_role",
                schema: "document_management",
                table: "business_link",
                columns: new[] { "document_id", "target_module", "target_type", "target_id", "link_role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_business_link_target",
                schema: "document_management",
                table: "business_link",
                columns: new[] { "target_module", "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_project_id_updated_at_id",
                schema: "document_management",
                table: "document",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_updated_at_id",
                schema: "document_management",
                table: "document",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_document_version_document_id_version_no",
                schema: "document_management",
                table: "document_version",
                columns: new[] { "document_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_version_scan_queue",
                schema: "document_management",
                table: "document_version",
                column: "updated_at",
                filter: "scan_state = 'SCAN_PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_document_version_storage_object_key",
                schema: "document_management",
                table: "document_version",
                column: "storage_object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evidence_reference_document_version_id",
                schema: "document_management",
                table: "evidence_reference",
                column: "document_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_reference_link_version_type",
                schema: "document_management",
                table: "evidence_reference",
                columns: new[] { "business_link_id", "document_version_id", "evidence_type_item_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evidence_reference",
                schema: "document_management");

            migrationBuilder.DropTable(
                name: "business_link",
                schema: "document_management");

            migrationBuilder.DropTable(
                name: "document_version",
                schema: "document_management");

            migrationBuilder.DropTable(
                name: "document",
                schema: "document_management");
        }
    }
}
