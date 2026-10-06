using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-057, 1 of 3: WF-07's tables in the <c>management_concern</c> schema, which TASK-024 created — the concern (issue or
    /// challenge, ERD §5.10), its impacts per dimension (ADR-011) and its escalations — with indexes I-16 to I-21 and the state facts as
    /// CHECK constraints. Foreign keys to other modules' tables are the next migration's (README R-7).
    /// </summary>
    public partial class TASK057_CreateManagementConcernTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "management_concern",
                schema: "management_concern",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concern_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    category_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    priority_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    overall_impact_level = table.Column<short>(type: "smallint", nullable: true),
                    severity_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    severity_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    raised_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    assignee_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    originating_risk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_resolution_date = table.Column<DateOnly>(type: "date", nullable: true),
                    next_review_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    description_lang = table.Column<string>(type: "char(2)", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    resolution_lang = table.Column<string>(type: "char(2)", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    title_lang = table.Column<string>(type: "char(2)", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_management_concern", x => x.id);
                    table.CheckConstraint("ck_management_concern_assigned", "status = 'OPEN' OR assignee_user_id IS NOT NULL");
                    table.CheckConstraint("ck_management_concern_closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");
                    table.CheckConstraint("ck_management_concern_concern_type", "\"concern_type\" IN ('ISSUE', 'CHALLENGE')");
                    table.CheckConstraint("ck_management_concern_description_lang", "\"description_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_management_concern_overall_impact_level", "overall_impact_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_management_concern_resolution", "status IN ('OPEN', 'ASSIGNED', 'IN_PROGRESS') OR resolution IS NOT NULL");
                    table.CheckConstraint("ck_management_concern_resolution_lang", "\"resolution_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_management_concern_resolution_pair", "(\"resolution\" IS NULL) = (\"resolution_lang\" IS NULL)");
                    table.CheckConstraint("ck_management_concern_resolved", "(status IN ('RESOLVED', 'CLOSED')) = (resolved_at IS NOT NULL)");
                    table.CheckConstraint("ck_management_concern_revision_no", "revision_no >= 1");
                    table.CheckConstraint("ck_management_concern_severity", "(severity_item_id IS NULL) = (severity_configuration_version_id IS NULL) AND (severity_item_id IS NULL) = (overall_impact_level IS NULL)");
                    table.CheckConstraint("ck_management_concern_status", "\"status\" IN ('OPEN', 'ASSIGNED', 'IN_PROGRESS', 'PENDING_VALIDATION', 'RESOLVED', 'CLOSED')");
                    table.CheckConstraint("ck_management_concern_title_lang", "\"title_lang\" IN ('ar', 'en')");
                });

            migrationBuilder.CreateTable(
                name: "concern_escalation",
                schema: "management_concern",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_concern_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escalation_no = table.Column<int>(type: "integer", nullable: false),
                    escalated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escalated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    escalated_to_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_key = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    reason_lang = table.Column<string>(type: "char(2)", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    resolution_lang = table.Column<string>(type: "char(2)", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concern_escalation", x => x.id);
                    table.CheckConstraint("ck_concern_escalation_ended", "(status = 'OPEN') = (resolved_at IS NULL) AND (status = 'OPEN') = (resolved_by_user_id IS NULL) AND (status = 'RESOLVED') = (resolution IS NOT NULL)");
                    table.CheckConstraint("ck_concern_escalation_escalation_no", "escalation_no >= 1");
                    table.CheckConstraint("ck_concern_escalation_reason_lang", "\"reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_concern_escalation_resolution_lang", "\"resolution_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_concern_escalation_resolution_pair", "(\"resolution\" IS NULL) = (\"resolution_lang\" IS NULL)");
                    table.CheckConstraint("ck_concern_escalation_status", "\"status\" IN ('OPEN', 'RESOLVED', 'WITHDRAWN')");
                    table.ForeignKey(
                        name: "fk_concern_escalation_management_concern_management_concern_id",
                        column: x => x.management_concern_id,
                        principalSchema: "management_concern",
                        principalTable: "management_concern",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "concern_impact",
                schema: "management_concern",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_concern_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("pk_concern_impact", x => x.id);
                    table.CheckConstraint("ck_concern_impact_impact_level", "impact_level BETWEEN 1 AND 5");
                    table.CheckConstraint("ck_concern_impact_rationale_lang", "\"rationale_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_concern_impact_rationale_pair", "(\"rationale\" IS NULL) = (\"rationale_lang\" IS NULL)");
                    table.ForeignKey(
                        name: "fk_concern_impact_management_concern_management_concern_id",
                        column: x => x.management_concern_id,
                        principalSchema: "management_concern",
                        principalTable: "management_concern",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_escalated_by_user_id_request_key",
                schema: "management_concern",
                table: "concern_escalation",
                columns: new[] { "escalated_by_user_id", "request_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_escalated_to_role_id_status",
                schema: "management_concern",
                table: "concern_escalation",
                columns: new[] { "escalated_to_role_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_management_concern_id_escalation_no",
                schema: "management_concern",
                table: "concern_escalation",
                columns: new[] { "management_concern_id", "escalation_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_one_open",
                schema: "management_concern",
                table: "concern_escalation",
                column: "management_concern_id",
                unique: true,
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "ix_concern_escalation_status_escalated_at_id",
                schema: "management_concern",
                table: "concern_escalation",
                columns: new[] { "status", "escalated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_concern_impact_concern_dimension",
                schema: "management_concern",
                table: "concern_impact",
                columns: new[] { "management_concern_id", "impact_dimension_item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_assignee_user_id_status",
                schema: "management_concern",
                table: "management_concern",
                columns: new[] { "assignee_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_concern_type_status_updated_at_id",
                schema: "management_concern",
                table: "management_concern",
                columns: new[] { "concern_type", "status", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_concern_type_updated_at_id",
                schema: "management_concern",
                table: "management_concern",
                columns: new[] { "concern_type", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_originating_risk_id",
                schema: "management_concern",
                table: "management_concern",
                column: "originating_risk_id");

            migrationBuilder.CreateIndex(
                name: "ix_management_concern_project_id_concern_type_updated_at_id",
                schema: "management_concern",
                table: "management_concern",
                columns: new[] { "project_id", "concern_type", "updated_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concern_escalation",
                schema: "management_concern");

            migrationBuilder.DropTable(
                name: "concern_impact",
                schema: "management_concern");

            migrationBuilder.DropTable(
                name: "management_concern",
                schema: "management_concern");
        }
    }
}
