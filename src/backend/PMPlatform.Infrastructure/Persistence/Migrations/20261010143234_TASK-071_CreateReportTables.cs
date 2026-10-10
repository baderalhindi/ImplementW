using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-071, 1 of 3: FG-02's twelve tables in the <c>reports</c> schema (ERD §5.20, with the additions of reports.md D-11). A definition's code
    /// is one of the ten ADR-006 delivers, so the column's CHECK refuses an eleventh report; one version of a report is on its way at a time; a
    /// column, a parameter and an option name registered fields and values by code, never an expression (BR-RPT-046); a saved view holds
    /// configuration only; a job holds its validated request; an output's key is opaque and its bytes sit apart, deleted when it is purged. The
    /// foreign keys that leave the schema are migration 2's (README R-7).
    /// </summary>
    public partial class TASK071_CreateReportTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_definition",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    audience_family = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    primary_projection_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    allows_saved_views = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    description_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    name_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    name_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    lifecycle_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    validated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    retired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_definition", x => x.id);
                    table.CheckConstraint("ck_report_definition_audience_family", "\"audience_family\" IN ('EXECUTIVE', 'PORTFOLIO', 'DEPARTMENT', 'PROJECT', 'FINANCIAL', 'PROGRESS', 'RISK_ISSUE')");
                    table.CheckConstraint("ck_report_definition_code", "\"code\" IN ('PORTFOLIO_SUMMARY', 'PROJECT_REGISTER', 'PROJECT_REPORT', 'PROGRESS_REPORTING', 'PROGRESS_HISTORY', 'SCHEDULE_DELIVERY', 'RISK_ISSUE', 'FINANCIAL_PERFORMANCE', 'KPI_PERFORMANCE', 'GOVERNANCE_CHANGE')");
                    table.CheckConstraint("ck_report_definition_description", "(\"description_ar\" IS NULL) = (\"description_en\" IS NULL)");
                    table.CheckConstraint("ck_report_definition_lifecycle_state", "\"lifecycle_state\" IN ('DRAFT', 'VALIDATED', 'PUBLISHED', 'RETIRED')");
                    table.CheckConstraint("ck_report_definition_primary_projection_code", "primary_projection_code ~ '^[A-Z][A-Z_]*\\.[A-Z][A-Z_]*$'");
                    table.CheckConstraint("ck_report_definition_version_no", "version_no >= 1");
                });

            migrationBuilder.CreateTable(
                name: "report_audience_role",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_audience_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_audience_role_report_definition_report_definition_id",
                        column: x => x.report_definition_id,
                        principalSchema: "reports",
                        principalTable: "report_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_column",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    is_default_visible = table.Column<bool>(type: "boolean", nullable: false),
                    data_classification_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_column", x => x.id);
                    table.CheckConstraint("ck_report_column_field_code", "field_code ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("ck_report_column_sort_order", "sort_order >= 1");
                    table.CheckConstraint("ck_report_column_source_entity_code", "source_entity_code ~ '^[A-Z][A-Z0-9_]*$'");
                    table.ForeignKey(
                        name: "fk_report_column_report_definition_report_definition_id",
                        column: x => x.report_definition_id,
                        principalSchema: "reports",
                        principalTable: "report_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_job",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    report_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    export_format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    report_language = table.Column<string>(type: "char(2)", nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    request_snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_job", x => x.id);
                    table.CheckConstraint("ck_report_job_export_format", "\"export_format\" IN ('PDF', 'XLSX', 'CSV')");
                    table.CheckConstraint("ck_report_job_failure_code", "(status = 'FAILED') = (failure_code IS NOT NULL) AND (failure_code IS NULL OR failure_code ~ '^[A-Z][A-Z0-9_]*$')");
                    table.CheckConstraint("ck_report_job_kind", "(kind = 'REPORT') = (report_definition_id IS NOT NULL)");
                    table.CheckConstraint("ck_report_job_report_language", "\"report_language\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_report_job_status", "\"status\" IN ('REQUESTED', 'VALIDATING', 'QUEUED', 'RUNNING', 'COMPLETED', 'FAILED', 'CANCELLED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "fk_report_job_report_definition_report_definition_id",
                        column: x => x.report_definition_id,
                        principalSchema: "reports",
                        principalTable: "report_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_parameter",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    data_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    source_entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_parameter", x => x.id);
                    table.CheckConstraint("ck_report_parameter_binding", "(data_type = 'OPTION') = (source_entity_code IS NOT NULL AND field_code IS NOT NULL) AND (source_entity_code IS NULL) = (field_code IS NULL)");
                    table.CheckConstraint("ck_report_parameter_code", "code ~ '^[A-Z][A-Z0-9_]*$'");
                    table.CheckConstraint("ck_report_parameter_data_type", "\"data_type\" IN ('PROJECT', 'DEPARTMENT', 'OPTION')");
                    table.CheckConstraint("ck_report_parameter_sort_order", "sort_order >= 1");
                    table.ForeignKey(
                        name: "fk_report_parameter_report_definition_report_definition_id",
                        column: x => x.report_definition_id,
                        principalSchema: "reports",
                        principalTable: "report_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_view",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    view_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    report_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    name_lang = table.Column<string>(type: "char(2)", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_view", x => x.id);
                    table.CheckConstraint("ck_saved_view_name_lang", "\"name_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_saved_view_report", "(view_type = 'REPORT_PARAMETERS') = (report_definition_id IS NOT NULL)");
                    table.CheckConstraint("ck_saved_view_view_type", "\"view_type\" IN ('REPORT_PARAMETERS', 'EXPLORER_COMPOSITION')");
                    table.ForeignKey(
                        name: "fk_saved_view_report_definition_report_definition_id",
                        column: x => x.report_definition_id,
                        principalSchema: "reports",
                        principalTable: "report_definition",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "generated_output",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    checksum_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    source_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    authorization_footprint = table.Column<string>(type: "jsonb", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    purged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_generated_output", x => x.id);
                    table.UniqueConstraint("ak_generated_output_storage_object_key", x => x.storage_object_key);
                    table.CheckConstraint("ck_generated_output_checksum_sha256", "checksum_sha256 ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_generated_output_file_name", "file_name ~ '^[A-Za-z0-9_-]+\\.(pdf|xlsx|csv)$'");
                    table.CheckConstraint("ck_generated_output_purged", "(status = 'PURGED') = (purged_at IS NOT NULL)");
                    table.CheckConstraint("ck_generated_output_sensitivity", "\"sensitivity\" IN ('STANDARD', 'SENSITIVE')");
                    table.CheckConstraint("ck_generated_output_size_bytes", "size_bytes >= 0 AND row_count >= 0");
                    table.CheckConstraint("ck_generated_output_status", "\"status\" IN ('AVAILABLE', 'EXPIRED', 'PURGED')");
                    table.CheckConstraint("ck_generated_output_storage_object_key", "storage_object_key ~ '^[0-9a-f]{32}$'");
                    table.ForeignKey(
                        name: "fk_generated_output_report_job_report_job_id",
                        column: x => x.report_job_id,
                        principalSchema: "reports",
                        principalTable: "report_job",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "report_parameter_option",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_parameter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    catalogue_entry_reference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    label_ar = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_parameter_option", x => x.id);
                    table.CheckConstraint("ck_report_parameter_option_catalogue_entry_reference", "catalogue_entry_reference ~ '^RPT-[A-Z]{3}-[0-9]{3}$'");
                    table.CheckConstraint("ck_report_parameter_option_value_code", "value_code ~ '^[A-Z][A-Z0-9_]*$'");
                    table.ForeignKey(
                        name: "fk_report_parameter_option_report_parameter_report_parameter_id",
                        column: x => x.report_parameter_id,
                        principalSchema: "reports",
                        principalTable: "report_parameter",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_parameter_value",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saved_view_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_parameter_value", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_parameter_value_saved_view_saved_view_id",
                        column: x => x.saved_view_id,
                        principalSchema: "reports",
                        principalTable: "saved_view",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_view_column",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saved_view_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_allowlist_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<short>(type: "smallint", nullable: false),
                    sort_direction = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_view_column", x => x.id);
                    table.CheckConstraint("ck_saved_view_column_sort_direction", "\"sort_direction\" IN ('ASC', 'DESC')");
                    table.CheckConstraint("ck_saved_view_column_sort_order", "sort_order >= 1");
                    table.ForeignKey(
                        name: "fk_saved_view_column_saved_view_saved_view_id",
                        column: x => x.saved_view_id,
                        principalSchema: "reports",
                        principalTable: "saved_view",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "saved_view_filter",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    saved_view_id = table.Column<Guid>(type: "uuid", nullable: false),
                    report_allowlist_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    @operator = table.Column<string>(name: "operator", type: "character varying(50)", maxLength: 50, nullable: false),
                    value_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_view_filter", x => x.id);
                    table.CheckConstraint("ck_saved_view_filter_operator", "\"operator\" IN ('EQ', 'NEQ', 'IN', 'GT', 'GTE', 'LT', 'LTE', 'BETWEEN', 'CONTAINS')");
                    table.ForeignKey(
                        name: "fk_saved_view_filter_saved_view_saved_view_id",
                        column: x => x.saved_view_id,
                        principalSchema: "reports",
                        principalTable: "saved_view",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "report_output_content",
                schema: "reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_report_output_content", x => x.id);
                    table.ForeignKey(
                        name: "fk_report_output_content_generated_output_storage_object_key",
                        column: x => x.storage_object_key,
                        principalSchema: "reports",
                        principalTable: "generated_output",
                        principalColumn: "storage_object_key",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_generated_output_report_job_id",
                schema: "reports",
                table: "generated_output",
                column: "report_job_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_generated_output_status_expires_at",
                schema: "reports",
                table: "generated_output",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_report_audience_role_report_definition_id_role_id",
                schema: "reports",
                table: "report_audience_role",
                columns: new[] { "report_definition_id", "role_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_column_report_definition_id_sort_order",
                schema: "reports",
                table: "report_column",
                columns: new[] { "report_definition_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_column_report_definition_id_source_entity_code_field",
                schema: "reports",
                table: "report_column",
                columns: new[] { "report_definition_id", "source_entity_code", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_definition_code_version_no",
                schema: "reports",
                table: "report_definition",
                columns: new[] { "code", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_definition_open_code",
                schema: "reports",
                table: "report_definition",
                column: "code",
                unique: true,
                filter: "lifecycle_state IN ('DRAFT', 'VALIDATED')");

            migrationBuilder.CreateIndex(
                name: "ix_report_definition_published",
                schema: "reports",
                table: "report_definition",
                column: "code",
                filter: "lifecycle_state = 'PUBLISHED'");

            migrationBuilder.CreateIndex(
                name: "ix_report_job_report_definition_id",
                schema: "reports",
                table: "report_job",
                column: "report_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_report_job_requested_by_user_id_idempotency_key",
                schema: "reports",
                table: "report_job",
                columns: new[] { "requested_by_user_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_job_requested_by_user_id_requested_at_id",
                schema: "reports",
                table: "report_job",
                columns: new[] { "requested_by_user_id", "requested_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_report_job_status_requested_at",
                schema: "reports",
                table: "report_job",
                columns: new[] { "status", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_report_output_content_storage_object_key",
                schema: "reports",
                table: "report_output_content",
                column: "storage_object_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_parameter_report_definition_id_code",
                schema: "reports",
                table: "report_parameter",
                columns: new[] { "report_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_parameter_option_report_parameter_id_value_code",
                schema: "reports",
                table: "report_parameter_option",
                columns: new[] { "report_parameter_id", "value_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_report_parameter_value_saved_view_id_parameter_code",
                schema: "reports",
                table: "report_parameter_value",
                columns: new[] { "saved_view_id", "parameter_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_owner_user_id_view_type",
                schema: "reports",
                table: "saved_view",
                columns: new[] { "owner_user_id", "view_type" });

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_report_definition_id",
                schema: "reports",
                table: "saved_view",
                column: "report_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_column_saved_view_id_report_allowlist_entry_id",
                schema: "reports",
                table: "saved_view_column",
                columns: new[] { "saved_view_id", "report_allowlist_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_column_saved_view_id_sort_order",
                schema: "reports",
                table: "saved_view_column",
                columns: new[] { "saved_view_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_filter_saved_view_id",
                schema: "reports",
                table: "saved_view_filter",
                column: "saved_view_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_audience_role",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_column",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_output_content",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_parameter_option",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_parameter_value",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "saved_view_column",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "saved_view_filter",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "generated_output",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_parameter",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "saved_view",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_job",
                schema: "reports");

            migrationBuilder.DropTable(
                name: "report_definition",
                schema: "reports");
        }
    }
}
