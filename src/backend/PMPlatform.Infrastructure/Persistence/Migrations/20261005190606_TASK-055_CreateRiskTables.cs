using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-055, 1 of 3: the WF-06 tables of ERD §5.9 <c>risk</c> — <c>risk</c>, <c>risk_assessment_version</c>,
    /// <c>risk_assessment_impact</c>, <c>risk_treatment_action</c> and <c>risk_acceptance</c> — with the foreign keys inside the
    /// schema, the state facts as CHECK constraints, indexes I-11 to I-15, one version number per risk, one impact per dimension of
    /// an assessment, and at most one ACTIVE acceptance per risk. The foreign keys to other modules' tables follow in 2 of 3
    /// (migrations README R-7).
    /// </summary>
    public partial class TASK055_CreateRiskTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "risk",
                schema: "risk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    identified_date = table.Column<DateOnly>(type: "date", nullable: false),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    materialised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reopened_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    closure_rationale_lang = table.Column<string>(type: "char(2)", nullable: true),
                    closure_rationale = table.Column<string>(type: "text", nullable: true),
                    description_lang = table.Column<string>(type: "char(2)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk", x => x.id);
                    table.CheckConstraint("ck_risk_closed", "(status = 'CLOSED') = (closed_at IS NOT NULL) AND (status = 'CLOSED') = (closed_by_user_id IS NOT NULL) AND (status = 'CLOSED') = (closure_rationale IS NOT NULL)");
                    table.CheckConstraint("ck_risk_closure_rationale_lang", "\"closure_rationale_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_closure_rationale_pair", "(\"closure_rationale\" IS NULL) = (\"closure_rationale_lang\" IS NULL)");
                    table.CheckConstraint("ck_risk_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_reopened_count", "reopened_count >= 0");
                    table.CheckConstraint("ck_risk_status", "\"status\" IN ('IDENTIFIED', 'ASSESSED', 'TREATMENT', 'MONITORING', 'CLOSED')");
                    table.CheckConstraint("ck_risk_title_lang", "\"title_lang\" IN ('ar', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "risk_acceptance",
                schema: "risk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    rationale_lang = table.Column<string>(type: "char(2)", nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_acceptance", x => x.id);
                    table.CheckConstraint("ck_risk_acceptance_expires_on", "expires_on > (accepted_at AT TIME ZONE 'UTC')::date");
                    table.CheckConstraint("ck_risk_acceptance_rationale_lang", "\"rationale_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_acceptance_revoked", "(status = 'REVOKED') = (revoked_at IS NOT NULL)");
                    table.CheckConstraint("ck_risk_acceptance_status", "\"status\" IN ('ACTIVE', 'EXPIRED', 'REVOKED')");
                    table.ForeignKey(
                        name: "fk_risk_acceptance_risk_risk_id",
                        column: x => x.risk_id,
                        principalSchema: "risk",
                        principalTable: "risk",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessment_version",
                schema: "risk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_no = table.Column<int>(type: "integer", nullable: false),
                    assessed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assessed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matrix_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    probability_level = table.Column<short>(type: "smallint", nullable: false),
                    overall_impact_level = table.Column<short>(type: "smallint", nullable: false),
                    risk_rating_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rationale_lang = table.Column<string>(type: "char(2)", nullable: true),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_assessment_version", x => x.id);
                    table.CheckConstraint("ck_risk_assessment_version_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_risk_assessment_version_overall_impact_level", "overall_impact_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risk_assessment_version_probability_level", "probability_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risk_assessment_version_rationale_lang", "\"rationale_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_assessment_version_rationale_pair", "(\"rationale\" IS NULL) = (\"rationale_lang\" IS NULL)");
                    table.CheckConstraint("ck_risk_assessment_version_version_no", "version_no >= 1");
                    table.ForeignKey(
                        name: "fk_risk_assessment_version_risk_risk_id",
                        column: x => x.risk_id,
                        principalSchema: "risk",
                        principalTable: "risk",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_treatment_action",
                schema: "risk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_risk_treatment_action", x => x.id);
                    table.CheckConstraint("ck_risk_treatment_action_action_type", "\"action_type\" IN ('MITIGATE', 'AVOID', 'TRANSFER', 'CONTINGENCY')");
                    table.CheckConstraint("ck_risk_treatment_action_completed", "(status = 'COMPLETED') = (completed_at IS NOT NULL)");
                    table.CheckConstraint("ck_risk_treatment_action_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_treatment_action_description_pair", "(\"description\" IS NULL) = (\"description_lang\" IS NULL)");
                    table.CheckConstraint("ck_risk_treatment_action_status", "\"status\" IN ('PLANNED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED')");
                    table.CheckConstraint("ck_risk_treatment_action_title_lang", "\"title_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_risk_treatment_action_risk_risk_id",
                        column: x => x.risk_id,
                        principalSchema: "risk",
                        principalTable: "risk",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_assessment_impact",
                schema: "risk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_assessment_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impact_dimension_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    impact_level = table.Column<short>(type: "smallint", nullable: false),
                    rationale_lang = table.Column<string>(type: "char(2)", nullable: true),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk_assessment_impact", x => x.id);
                    table.CheckConstraint("ck_risk_assessment_impact_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_risk_assessment_impact_impact_level", "impact_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_risk_assessment_impact_rationale_lang", "\"rationale_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_risk_assessment_impact_rationale_pair", "(\"rationale\" IS NULL) = (\"rationale_lang\" IS NULL)");
                    table.ForeignKey(
                        name: "fk_risk_assessment_impact_risk_assessment_version_risk_assessm",
                        column: x => x.risk_assessment_version_id,
                        principalSchema: "risk",
                        principalTable: "risk_assessment_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_risk_next_review_date",
                schema: "risk",
                table: "risk",
                column: "next_review_date");

            migrationBuilder.CreateIndex(
                name: "ix_risk_owner_user_id_status",
                schema: "risk",
                table: "risk",
                columns: new[] { "owner_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_project_id_updated_at_id",
                schema: "risk",
                table: "risk",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_status_updated_at_id",
                schema: "risk",
                table: "risk",
                columns: new[] { "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_updated_at_id",
                schema: "risk",
                table: "risk",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_acceptance_active_risk_id",
                schema: "risk",
                table: "risk_acceptance",
                column: "risk_id",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_risk_acceptance_status_expires_on",
                schema: "risk",
                table: "risk_acceptance",
                columns: new[] { "status", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_impact_risk_assessment_version_id_impact_di",
                schema: "risk",
                table: "risk_assessment_impact",
                columns: new[] { "risk_assessment_version_id", "impact_dimension_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_assessment_version_risk_id_version_no",
                schema: "risk",
                table: "risk_assessment_version",
                columns: new[] { "risk_id", "version_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_treatment_action_risk_id",
                schema: "risk",
                table: "risk_treatment_action",
                column: "risk_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "risk_acceptance",
                schema: "risk");

            migrationBuilder.DropTable(
                name: "risk_assessment_impact",
                schema: "risk");

            migrationBuilder.DropTable(
                name: "risk_treatment_action",
                schema: "risk");

            migrationBuilder.DropTable(
                name: "risk_assessment_version",
                schema: "risk");

            migrationBuilder.DropTable(
                name: "risk",
                schema: "risk");
        }
    }
}
