using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-060, 1 of 3: WF-08's tables in the <c>change_request</c> schema, which TASK-024 created — the change request (ERD §5.11), the
    /// materiality evaluation of each reviewed revision with the commitments it pinned (ADR-016), and the change authorisations approval
    /// issues, unique by issuance key — with indexes I-26 to I-28 and the state facts as CHECK constraints. Foreign keys to other modules'
    /// tables are the next migration's (README R-7).
    /// </summary>
    public partial class TASK060_CreateChangeRequestTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "change_request",
                schema: "change_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cost_impact_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    schedule_impact_days = table.Column<int>(type: "integer", nullable: true),
                    is_contractual_obligation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    requested_governance_profile_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    implemented_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    justification_lang = table.Column<string>(type: "char(2)", nullable: false),
                    justification = table.Column<string>(type: "text", nullable: false),
                    scope_impact_lang = table.Column<string>(type: "char(2)", nullable: true),
                    scope_impact = table.Column<string>(type: "text", nullable: true),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_request", x => x.id);
                    table.CheckConstraint("ck_change_request_change_type", "\"change_type\" IN ('SCOPE', 'COST', 'SCHEDULE', 'CONTRACTUAL_OBLIGATION', 'GOVERNANCE_PROFILE')");
                    table.CheckConstraint("ck_change_request_closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");
                    table.CheckConstraint("ck_change_request_contractual", "change_type <> 'CONTRACTUAL_OBLIGATION' OR is_contractual_obligation");
                    table.CheckConstraint("ck_change_request_implemented", "(status IN ('IMPLEMENTED', 'CLOSED')) = (implemented_at IS NOT NULL)");
                    table.CheckConstraint("ck_change_request_justification_lang", "\"justification_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_change_request_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_change_request_scope_impact_lang", "\"scope_impact_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_change_request_scope_impact_pair", "(\"scope_impact\" IS NULL) = (\"scope_impact_lang\" IS NULL)");
                    table.CheckConstraint("ck_change_request_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'APPROVED', 'REJECTED', 'IMPLEMENTATION', 'IMPLEMENTED', 'CLOSED', 'WITHDRAWN')");
                    table.CheckConstraint("ck_change_request_submitted", "(status = 'DRAFT') = (submitted_at IS NULL)");
                    table.CheckConstraint("ck_change_request_title_lang", "\"title_lang\" IN ('ar', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "change_authorization",
                schema: "change_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authorization_scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_revision_no = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    applied_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applied_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_authorization", x => x.id);
                    table.CheckConstraint("ck_change_authorization_applied", "(status = 'APPLIED') = (applied_at IS NOT NULL) AND (status = 'APPLIED') = (applied_by_user_id IS NOT NULL) AND (status = 'APPLIED') = (applied_reference IS NOT NULL)");
                    table.CheckConstraint("ck_change_authorization_authorization_scope", "\"authorization_scope\" IN ('REBASELINE', 'COMMITMENT_CHANGE', 'PROFILE_CHANGE', 'SCOPE_CHANGE')");
                    table.CheckConstraint("ck_change_authorization_expires_at", "expires_at IS NULL OR expires_at > issued_at");
                    table.CheckConstraint("ck_change_authorization_status", "\"status\" IN ('ISSUED', 'APPLIED', 'EXPIRED', 'REVOKED')");
                    table.CheckConstraint("ck_change_authorization_target_revision_no", "target_revision_no >= 1");
                    table.ForeignKey(
                        name: "fk_change_authorization_change_request_change_request_id",
                        column: x => x.change_request_id,
                        principalSchema: "change_request",
                        principalTable: "change_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "materiality_evaluation",
                schema: "change_request",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    materiality_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_baseline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_baseline_version_no = table.Column<int>(type: "integer", nullable: true),
                    baseline_duration_days = table.Column<int>(type: "integer", nullable: true),
                    financial_commitment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    financial_commitment_version_no = table.Column<int>(type: "integer", nullable: true),
                    baseline_budget_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cumulative_cost_impact_sar = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cumulative_schedule_impact_days = table.Column<int>(type: "integer", nullable: false),
                    cost_band_no = table.Column<short>(type: "smallint", nullable: true),
                    schedule_band_no = table.Column<short>(type: "smallint", nullable: true),
                    scope_band_no = table.Column<short>(type: "smallint", nullable: true),
                    resulting_band_no = table.Column<short>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materiality_evaluation", x => x.id);
                    table.CheckConstraint("ck_materiality_evaluation_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_materiality_evaluation_bands", "resulting_band_no BETWEEN 1 AND 3 AND (cost_band_no IS NULL OR cost_band_no BETWEEN 1 AND resulting_band_no) AND (schedule_band_no IS NULL OR schedule_band_no BETWEEN 1 AND resulting_band_no) AND (scope_band_no IS NULL OR scope_band_no BETWEEN 1 AND resulting_band_no)");
                    table.CheckConstraint("ck_materiality_evaluation_baseline_pin", "(project_baseline_id IS NULL) = (project_baseline_version_no IS NULL) AND (project_baseline_id IS NULL) = (baseline_duration_days IS NULL) AND (schedule_band_no IS NULL OR project_baseline_id IS NOT NULL) AND (baseline_duration_days IS NULL OR baseline_duration_days >= 1)");
                    table.CheckConstraint("ck_materiality_evaluation_budget_pin", "(financial_commitment_id IS NULL) = (financial_commitment_version_no IS NULL) AND (financial_commitment_id IS NULL) = (baseline_budget_sar IS NULL) AND (cost_band_no IS NULL) = (financial_commitment_id IS NULL)");
                    table.CheckConstraint("ck_materiality_evaluation_revision_no", "revision_no >= 1");
                    table.ForeignKey(
                        name: "fk_materiality_evaluation_change_request_change_request_id",
                        column: x => x.change_request_id,
                        principalSchema: "change_request",
                        principalTable: "change_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_change_authorization_change_request_id_issued_at",
                schema: "change_request",
                table: "change_authorization",
                columns: new[] { "change_request_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_change_authorization_idempotency_key",
                schema: "change_request",
                table: "change_authorization",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_request_project_id_updated_at_id",
                schema: "change_request",
                table: "change_request",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_change_request_status_updated_at_id",
                schema: "change_request",
                table: "change_request",
                columns: new[] { "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_change_request_updated_at_id",
                schema: "change_request",
                table: "change_request",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_materiality_evaluation_change_request_id_revision_no",
                schema: "change_request",
                table: "materiality_evaluation",
                columns: new[] { "change_request_id", "revision_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "change_authorization",
                schema: "change_request");

            migrationBuilder.DropTable(
                name: "materiality_evaluation",
                schema: "change_request");

            migrationBuilder.DropTable(
                name: "change_request",
                schema: "change_request");
        }
    }
}
