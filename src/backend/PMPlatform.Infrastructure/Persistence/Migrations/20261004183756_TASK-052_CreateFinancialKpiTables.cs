using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-052, 1 of 3: WF-14's nine tables in the <c>financial_kpi</c> schema (ERD §5.17), with their keys within the schema,
    /// the partial unique indexes that hold one ACTIVE and one open version, and indexes I-39 and I-40. The keys that leave the
    /// schema are 2 of 3 (README R-7).
    /// </summary>
    public partial class TASK052_CreateFinancialKpiTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "financial_commitment",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    commitment_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    amount_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_by_commitment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    change_authorization_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_commitment", x => x.id);
                    table.CheckConstraint("ck_financial_commitment_activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
                    table.CheckConstraint("ck_financial_commitment_amount", "amount_sar >= 0");
                    table.CheckConstraint("ck_financial_commitment_commitment_type", "\"commitment_type\" IN ('APPROVED_BUDGET', 'DECLARED_BUDGET', 'OPEN_COMMITMENT')");
                    table.CheckConstraint("ck_financial_commitment_intake", "(commitment_type = 'DECLARED_BUDGET') = (project_intake_id IS NOT NULL)");
                    table.CheckConstraint("ck_financial_commitment_numbers", "version_no >= 1 AND revision_no >= 1");
                    table.CheckConstraint("ck_financial_commitment_source_reference", "source_type = 'MANUAL' OR source_reference IS NOT NULL");
                    table.CheckConstraint("ck_financial_commitment_source_type", "\"source_type\" IN ('MANUAL', 'ETIMAD', 'OTHER')");
                    table.CheckConstraint("ck_financial_commitment_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'ACTIVE', 'SUPERSEDED', 'REJECTED', 'WITHDRAWN')");
                    table.CheckConstraint("ck_financial_commitment_superseded", "(status = 'SUPERSEDED') = (superseded_by_commitment_id IS NOT NULL) AND (superseded_by_commitment_id IS NULL OR superseded_by_commitment_id <> id)");
                    table.ForeignKey(
                        name: "fk_financial_commitment_financial_commitment_superseded_by_com",
                        column: x => x.superseded_by_commitment_id,
                        principalSchema: "financial_kpi",
                        principalTable: "financial_commitment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_progress_update",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporting_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actual_expenditure_to_date_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    forecast_at_completion_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    value_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    project_intake_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    narrative = table.Column<string>(type: "text", nullable: true),
                    return_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    return_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_progress_update", x => x.id);
                    table.CheckConstraint("ck_financial_progress_update_amounts", "(actual_expenditure_to_date_sar IS NULL OR actual_expenditure_to_date_sar >= 0) AND (forecast_at_completion_sar IS NULL OR forecast_at_completion_sar >= 0)");
                    table.CheckConstraint("ck_financial_progress_update_forecast", "forecast_at_completion_sar IS NULL OR actual_expenditure_to_date_sar IS NOT NULL");
                    table.CheckConstraint("ck_financial_progress_update_narrative_lang", "\"narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_financial_progress_update_narrative_pair", "(\"narrative\" IS NULL) = (\"narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_financial_progress_update_return_reason", "(status = 'RETURNED') = (return_reason IS NOT NULL)");
                    table.CheckConstraint("ck_financial_progress_update_return_reason_lang", "\"return_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_financial_progress_update_return_reason_pair", "(\"return_reason\" IS NULL) = (\"return_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_financial_progress_update_reviewed", "(status IN ('RETURNED', 'PUBLISHED')) = (reviewed_at IS NOT NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_financial_progress_update_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_financial_progress_update_source_reference", "source_type = 'MANUAL' OR source_reference IS NOT NULL");
                    table.CheckConstraint("ck_financial_progress_update_source_type", "\"source_type\" IN ('MANUAL', 'ETIMAD', 'OTHER')");
                    table.CheckConstraint("ck_financial_progress_update_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'PUBLISHED')");
                    table.CheckConstraint("ck_financial_progress_update_submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (submitted_at IS NULL) = (submitted_by_user_id IS NULL)");
                    table.CheckConstraint("ck_financial_progress_update_value_status", "(value_status = 'MEASURED') = (actual_expenditure_to_date_sar IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "financial_source_mode",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    configured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_source_mode", x => x.id);
                    table.CheckConstraint("ck_financial_source_mode_field_code", "\"field_code\" IN ('APPROVED_BUDGET', 'ACTUAL_EXPENDITURE', 'FORECAST_AT_COMPLETION', 'OPEN_COMMITMENT')");
                    table.CheckConstraint("ck_financial_source_mode_source_mode", "\"source_mode\" IN ('MANUAL', 'INTEGRATED', 'HYBRID')");
                });

            migrationBuilder.CreateTable(
                name: "kpi_assignment",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measurement_frequency_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_assignment", x => x.id);
                    table.CheckConstraint("ck_kpi_assignment_status", "\"status\" IN ('ACTIVE', 'SUSPENDED', 'RETIRED')");
                });

            migrationBuilder.CreateTable(
                name: "financial_commitment_line",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_commitment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etimad_category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_commitment_line", x => x.id);
                    table.CheckConstraint("ck_financial_commitment_line_amount", "amount_sar >= 0");
                    table.ForeignKey(
                        name: "fk_financial_commitment_line_financial_commitment_financial_co",
                        column: x => x.financial_commitment_id,
                        principalSchema: "financial_kpi",
                        principalTable: "financial_commitment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "financial_progress_update_line",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_progress_update_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etimad_category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actual_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_financial_progress_update_line", x => x.id);
                    table.CheckConstraint("ck_financial_progress_update_line_amount", "actual_sar >= 0");
                    table.ForeignKey(
                        name: "fk_financial_progress_update_line_financial_progress_update_fi",
                        column: x => x.financial_progress_update_id,
                        principalSchema: "financial_kpi",
                        principalTable: "financial_progress_update",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "published_financial_snapshot",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reporting_cycle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_progress_update_id = table.Column<Guid>(type: "uuid", nullable: false),
                    financial_commitment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_budget_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    actual_expenditure_to_date_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    forecast_at_completion_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    value_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    financial_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    threshold_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    entered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_published_financial_snapshot", x => x.id);
                    table.CheckConstraint("ck_published_financial_snapshot_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_published_financial_snapshot_budget", "(financial_commitment_id IS NULL) = (approved_budget_sar IS NULL)");
                    table.CheckConstraint("ck_published_financial_snapshot_financial_status", "financial_status = 'UNKNOWN' OR (value_status = 'MEASURED' AND approved_budget_sar > 0 AND forecast_at_completion_sar IS NOT NULL)");
                    table.CheckConstraint("ck_published_financial_snapshot_source_type", "\"source_type\" IN ('MANUAL', 'ETIMAD', 'OTHER')");
                    table.CheckConstraint("ck_published_financial_snapshot_value_status", "(value_status = 'MEASURED') = (actual_expenditure_to_date_sar IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_published_financial_snapshot_financial_commitment_financial",
                        column: x => x.financial_commitment_id,
                        principalSchema: "financial_kpi",
                        principalTable: "financial_commitment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_published_financial_snapshot_financial_progress_update_fina",
                        column: x => x.financial_progress_update_id,
                        principalSchema: "financial_kpi",
                        principalTable: "financial_progress_update",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kpi_target_version",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    green_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    amber_threshold = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    superseded_by_target_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_target_version", x => x.id);
                    table.CheckConstraint("ck_kpi_target_version_activated", "(status IN ('ACTIVE', 'SUPERSEDED')) = (activated_at IS NOT NULL)");
                    table.CheckConstraint("ck_kpi_target_version_numbers", "version_no >= 1 AND revision_no >= 1");
                    table.CheckConstraint("ck_kpi_target_version_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'ACTIVE', 'SUPERSEDED', 'REJECTED', 'WITHDRAWN')");
                    table.CheckConstraint("ck_kpi_target_version_superseded", "(status = 'SUPERSEDED') = (superseded_by_target_version_id IS NOT NULL) AND (superseded_by_target_version_id IS NULL OR superseded_by_target_version_id <> id)");
                    table.CheckConstraint("ck_kpi_target_version_thresholds", "(green_threshold IS NULL) = (amber_threshold IS NULL)");
                    table.ForeignKey(
                        name: "fk_kpi_target_version_kpi_assignment_kpi_assignment_id",
                        column: x => x.kpi_assignment_id,
                        principalSchema: "financial_kpi",
                        principalTable: "kpi_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kpi_target_version_kpi_target_version_superseded_by_target_",
                        column: x => x.superseded_by_target_version_id,
                        principalSchema: "financial_kpi",
                        principalTable: "kpi_target_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "kpi_measurement",
                schema: "financial_kpi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kpi_target_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    measured_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    value_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rag_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    as_of_date = table.Column<DateOnly>(type: "date", nullable: false),
                    recorded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    narrative_lang = table.Column<string>(type: "char(2)", nullable: true),
                    narrative = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_kpi_measurement", x => x.id);
                    table.CheckConstraint("ck_kpi_measurement_narrative_lang", "\"narrative_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_kpi_measurement_narrative_pair", "(\"narrative\" IS NULL) = (\"narrative_lang\" IS NULL)");
                    table.CheckConstraint("ck_kpi_measurement_period", "period_end >= period_start");
                    table.CheckConstraint("ck_kpi_measurement_published", "(status = 'PUBLISHED') = (published_at IS NOT NULL) AND (published_at IS NULL) = (published_by_user_id IS NULL)");
                    table.CheckConstraint("ck_kpi_measurement_rag_status", "(value_status = 'NOT_APPLICABLE') = (rag_status = 'NOT_APPLICABLE') AND (rag_status NOT IN ('GREEN', 'AMBER', 'RED') OR measured_value IS NOT NULL)");
                    table.CheckConstraint("ck_kpi_measurement_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'PUBLISHED')");
                    table.CheckConstraint("ck_kpi_measurement_submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
                    table.CheckConstraint("ck_kpi_measurement_value_status", "(value_status = 'MEASURED') = (measured_value IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_kpi_measurement_kpi_assignment_kpi_assignment_id",
                        column: x => x.kpi_assignment_id,
                        principalSchema: "financial_kpi",
                        principalTable: "kpi_assignment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_kpi_measurement_kpi_target_version_kpi_target_version_id",
                        column: x => x.kpi_target_version_id,
                        principalSchema: "financial_kpi",
                        principalTable: "kpi_target_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_active_project_id_commitment_type",
                schema: "financial_kpi",
                table: "financial_commitment",
                columns: new[] { "project_id", "commitment_type" },
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_open_project_id_commitment_type",
                schema: "financial_kpi",
                table: "financial_commitment",
                columns: new[] { "project_id", "commitment_type" },
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED')");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_project_id_commitment_type_version_no",
                schema: "financial_kpi",
                table: "financial_commitment",
                columns: new[] { "project_id", "commitment_type", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_superseded_by_commitment_id",
                schema: "financial_kpi",
                table: "financial_commitment",
                column: "superseded_by_commitment_id");

            migrationBuilder.CreateIndex(
                name: "ix_financial_commitment_line_financial_commitment_id_etimad_ca",
                schema: "financial_kpi",
                table: "financial_commitment_line",
                columns: new[] { "financial_commitment_id", "etimad_category_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_open_reporting_cycle_id",
                schema: "financial_kpi",
                table: "financial_progress_update",
                column: "reporting_cycle_id",
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW')");

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_project_id_as_of_date",
                schema: "financial_kpi",
                table: "financial_progress_update",
                columns: new[] { "project_id", "as_of_date" });

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_reporting_cycle_id_revision_no",
                schema: "financial_kpi",
                table: "financial_progress_update",
                columns: new[] { "reporting_cycle_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_progress_update_line_financial_progress_update_id",
                schema: "financial_kpi",
                table: "financial_progress_update_line",
                columns: new[] { "financial_progress_update_id", "etimad_category_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_financial_source_mode_project_id_field_code",
                schema: "financial_kpi",
                table: "financial_source_mode",
                columns: new[] { "project_id", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_assignment_project_id_kpi_definition_id",
                schema: "financial_kpi",
                table: "kpi_assignment",
                columns: new[] { "project_id", "kpi_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_measurement_kpi_assignment_id_period_start",
                schema: "financial_kpi",
                table: "kpi_measurement",
                columns: new[] { "kpi_assignment_id", "period_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_measurement_kpi_target_version_id",
                schema: "financial_kpi",
                table: "kpi_measurement",
                column: "kpi_target_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_target_version_active_kpi_assignment_id",
                schema: "financial_kpi",
                table: "kpi_target_version",
                column: "kpi_assignment_id",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_target_version_kpi_assignment_id_version_no",
                schema: "financial_kpi",
                table: "kpi_target_version",
                columns: new[] { "kpi_assignment_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_kpi_target_version_open_kpi_assignment_id",
                schema: "financial_kpi",
                table: "kpi_target_version",
                column: "kpi_assignment_id",
                unique: true,
                filter: "status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED')");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_target_version_superseded_by_target_version_id",
                schema: "financial_kpi",
                table: "kpi_target_version",
                column: "superseded_by_target_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_financial_commitment_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "financial_commitment_id");

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_financial_progress_update_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "financial_progress_update_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_project_id_as_of_date",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                columns: new[] { "project_id", "as_of_date" });

            migrationBuilder.CreateIndex(
                name: "ix_published_financial_snapshot_reporting_cycle_id",
                schema: "financial_kpi",
                table: "published_financial_snapshot",
                column: "reporting_cycle_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "financial_commitment_line",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "financial_progress_update_line",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "financial_source_mode",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "kpi_measurement",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "published_financial_snapshot",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "kpi_target_version",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "financial_commitment",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "financial_progress_update",
                schema: "financial_kpi");

            migrationBuilder.DropTable(
                name: "kpi_assignment",
                schema: "financial_kpi");
        }
    }
}
