using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-066, 1 of 3: WF-13's four tables in the <c>external_participation</c> schema, as the ERD (§5.16) has them with the changes
    /// external-participation.md D-2 records: the request carries the source record and the pinned schema; a revision is one row, numbered
    /// within its request and corrected by one revision only; a revision's values are unique per field; an application attempt is numbered
    /// within its revision, unique by its key, and at most one per revision is APPLIED (EXT-CC-18). Indexes I-41 to I-45 (indexing-strategy.md)
    /// and the register's. Foreign keys to other modules' tables are migration 2's; the history guard is migration 3's.
    /// </summary>
    public partial class TASK066_CreateExternalParticipationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_update_request",
                schema: "external_participation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    contribution_type_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contribution_schema_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    participation_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    target_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responsible_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    cancellation_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    cancellation_reason = table.Column<string>(type: "text", nullable: true),
                    instructions_lang = table.Column<string>(type: "char(2)", nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_update_request", x => x.id);
                    table.CheckConstraint("ck_external_update_request_cancellation_reason_lang", "\"cancellation_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_external_update_request_cancellation_reason_pair", "(\"cancellation_reason\" IS NULL) = (\"cancellation_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_external_update_request_cancelled", "(status = 'CANCELLED') = (cancelled_at IS NOT NULL) AND (cancelled_at IS NULL) = (cancellation_reason IS NULL)");
                    table.CheckConstraint("ck_external_update_request_closed", "(status = 'CLOSED') = (closed_at IS NOT NULL)");
                    table.CheckConstraint("ck_external_update_request_instructions_lang", "\"instructions_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_external_update_request_issued", "(status = 'DRAFT') = (issued_at IS NULL) AND (issued_at IS NULL) = (issued_by_user_id IS NULL) AND (status = 'DRAFT') = (participation_configuration_version_id IS NULL)");
                    table.CheckConstraint("ck_external_update_request_origin", "\"origin\" IN ('AHDA_ISSUED')");
                    table.CheckConstraint("ck_external_update_request_people", "status = 'DRAFT' OR (responsible_user_id IS NOT NULL AND reviewer_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_external_update_request_status", "\"status\" IN ('DRAFT', 'ISSUED', 'IN_PROGRESS', 'RESPONDED', 'CLOSED', 'CANCELLED')");
                    table.CheckConstraint("ck_external_update_request_target", "(target_id IS NULL) = (target_type IS NULL) AND (target_id IS NULL) = (target_module IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "external_contribution",
                schema: "external_participation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_update_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contributor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_no = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    target_version = table.Column<long>(type: "bigint", nullable: true),
                    target_state = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    review_internal_note_lang = table.Column<string>(type: "char(2)", nullable: true),
                    review_internal_note = table.Column<string>(type: "text", nullable: true),
                    review_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    review_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_contribution", x => x.id);
                    table.CheckConstraint("ck_external_contribution_review_internal_note_lang", "\"review_internal_note_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_external_contribution_review_internal_note_pair", "(\"review_internal_note\" IS NULL) = (\"review_internal_note_lang\" IS NULL)");
                    table.CheckConstraint("ck_external_contribution_review_reason", "status NOT IN ('RETURNED', 'REJECTED') OR review_reason IS NOT NULL");
                    table.CheckConstraint("ck_external_contribution_review_reason_lang", "\"review_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_external_contribution_review_reason_pair", "(\"review_reason\" IS NULL) = (\"review_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_external_contribution_review_started", "(status IN ('DRAFT', 'SUBMITTED')) = (review_started_at IS NULL)");
                    table.CheckConstraint("ck_external_contribution_reviewed", "(status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW')) = (reviewed_at IS NULL) AND (reviewed_at IS NULL) = (reviewed_by_user_id IS NULL)");
                    table.CheckConstraint("ck_external_contribution_revision_no", "revision_no >= 1 AND (revision_no = 1) = (previous_revision_id IS NULL)");
                    table.CheckConstraint("ck_external_contribution_status", "\"status\" IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED', 'REJECTED', 'ACCEPTED_PENDING_APPLICATION', 'APPLIED', 'APPLICATION_FAILED')");
                    table.CheckConstraint("ck_external_contribution_submitted", "(status = 'DRAFT') = (submitted_at IS NULL) AND (target_version IS NULL) = (target_state IS NULL)");
                    table.ForeignKey(
                        name: "fk_external_contribution_external_contribution_previous_revisi",
                        column: x => x.previous_revision_id,
                        principalSchema: "external_participation",
                        principalTable: "external_contribution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_contribution_external_update_request_external_upda",
                        column: x => x.external_update_request_id,
                        principalSchema: "external_participation",
                        principalTable: "external_update_request",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "external_contribution_field",
                schema: "external_participation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_contribution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    proposed_value = table.Column<string>(type: "text", nullable: false),
                    proposed_value_lang = table.Column<string>(type: "char(2)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_contribution_field", x => x.id);
                    table.CheckConstraint("ck_external_contribution_field_proposed_value_lang", "\"proposed_value_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_external_contribution_field_external_contribution_external_",
                        column: x => x.external_contribution_id,
                        principalSchema: "external_participation",
                        principalTable: "external_contribution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "source_application",
                schema: "external_participation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_contribution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    expected_target_revision_no = table.Column<long>(type: "bigint", nullable: true),
                    actual_target_revision_no = table.Column<long>(type: "bigint", nullable: true),
                    attempted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    revalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revalidated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revalidated_target_revision_no = table.Column<long>(type: "bigint", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_application", x => x.id);
                    table.CheckConstraint("ck_source_application_attempt_no", "attempt_no >= 1 AND completed_at >= attempted_at");
                    table.CheckConstraint("ck_source_application_outcome", "(status = 'CONFLICT') = (actual_target_revision_no IS NOT NULL) AND (status = 'FAILED') = (failure_code IS NOT NULL)");
                    table.CheckConstraint("ck_source_application_revalidated", "(revalidated_at IS NULL OR status = 'CONFLICT') AND (revalidated_at IS NULL) = (revalidated_by_user_id IS NULL) AND (revalidated_at IS NULL) = (revalidated_target_revision_no IS NULL)");
                    table.CheckConstraint("ck_source_application_status", "\"status\" IN ('APPLIED', 'CONFLICT', 'FAILED')");
                    table.ForeignKey(
                        name: "fk_source_application_external_contribution_external_contribut",
                        column: x => x.external_contribution_id,
                        principalSchema: "external_participation",
                        principalTable: "external_contribution",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_contributor_user_id_submitted_at_id",
                schema: "external_participation",
                table: "external_contribution",
                columns: new[] { "contributor_user_id", "submitted_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_external_update_request_id_revision_no",
                schema: "external_participation",
                table: "external_contribution",
                columns: new[] { "external_update_request_id", "revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_previous_revision_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "previous_revision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_reviewed_by_user_id",
                schema: "external_participation",
                table: "external_contribution",
                column: "reviewed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_status_submitted_at_id",
                schema: "external_participation",
                table: "external_contribution",
                columns: new[] { "status", "submitted_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_external_contribution_field_external_contribution_id_field_",
                schema: "external_participation",
                table: "external_contribution_field",
                columns: new[] { "external_contribution_id", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_external_entity_id_status_due_date",
                schema: "external_participation",
                table: "external_update_request",
                columns: new[] { "external_entity_id", "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_project_id_issued_at_id",
                schema: "external_participation",
                table: "external_update_request",
                columns: new[] { "project_id", "issued_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_project_id_updated_at_id",
                schema: "external_participation",
                table: "external_update_request",
                columns: new[] { "project_id", "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_responsible_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "responsible_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_reviewer_user_id",
                schema: "external_participation",
                table: "external_update_request",
                column: "reviewer_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_update_request_updated_at_id",
                schema: "external_participation",
                table: "external_update_request",
                columns: new[] { "updated_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_source_application_applied_external_contribution_id",
                schema: "external_participation",
                table: "source_application",
                column: "external_contribution_id",
                unique: true,
                filter: "status = 'APPLIED'");

            migrationBuilder.CreateIndex(
                name: "ix_source_application_attempted_by_user_id",
                schema: "external_participation",
                table: "source_application",
                column: "attempted_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_application_external_contribution_id_attempt_no",
                schema: "external_participation",
                table: "source_application",
                columns: new[] { "external_contribution_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_application_idempotency_key",
                schema: "external_participation",
                table: "source_application",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_application_revalidated_by_user_id",
                schema: "external_participation",
                table: "source_application",
                column: "revalidated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_application_status_attempted_at",
                schema: "external_participation",
                table: "source_application",
                columns: new[] { "status", "attempted_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_contribution_field",
                schema: "external_participation");

            migrationBuilder.DropTable(
                name: "source_application",
                schema: "external_participation");

            migrationBuilder.DropTable(
                name: "external_contribution",
                schema: "external_participation");

            migrationBuilder.DropTable(
                name: "external_update_request",
                schema: "external_participation");
        }
    }
}
