using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-035, 2 of 4: the WF-11 tables of ERD §5 <c>approval</c> — <c>approval_instance</c>, <c>approval_task</c>,
    /// <c>approval_delegation</c> — with the foreign keys inside the schema and the register indexes I-22 to I-25. The
    /// foreign keys to other modules' tables follow in 3 of 4 (migrations README R-7).
    /// </summary>
    public partial class TASK035_CreateApprovalTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approval_delegation",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegate_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    routing_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_delegation", x => x.id);
                    table.CheckConstraint("ck_approval_delegation_period", "valid_to > valid_from");
                    table.CheckConstraint("ck_approval_delegation_status", "\"status\" IN ('ACTIVE', 'REVOKED', 'EXPIRED')");
                });

            migrationBuilder.CreateTable(
                name: "approval_instance",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_module = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_revision_no = table.Column<int>(type: "integer", nullable: false),
                    routing_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    authority_configuration_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_department_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    outcome_idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    outcome_delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    previous_instance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_instance", x => x.id);
                    table.CheckConstraint("ck_approval_instance_status", "\"status\" IN ('PENDING', 'APPROVED', 'REJECTED', 'RETURNED', 'WITHDRAWN')");
                    table.ForeignKey(
                        name: "fk_approval_instance_approval_instance_previous_instance_id",
                        column: x => x.previous_instance_id,
                        principalSchema: "approval",
                        principalTable: "approval_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_task",
                schema: "approval",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    approval_instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<short>(type: "smallint", nullable: false),
                    assigned_role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acting_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_delegation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eligibility_revalidated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    escalated_to_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_task", x => x.id);
                    table.CheckConstraint("ck_approval_task_decision_reason_lang", "\"decision_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_approval_task_decision_reason_pair", "(\"decision_reason\" IS NULL) = (\"decision_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_approval_task_status", "\"status\" IN ('PENDING', 'APPROVED', 'REJECTED', 'RETURNED', 'DELEGATED', 'ESCALATED', 'CANCELLED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "fk_approval_task_approval_delegation_approval_delegation_id",
                        column: x => x.approval_delegation_id,
                        principalSchema: "approval",
                        principalTable: "approval_delegation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_task_approval_instance_approval_instance_id",
                        column: x => x.approval_instance_id,
                        principalSchema: "approval",
                        principalTable: "approval_instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_task_approval_task_escalated_to_task_id",
                        column: x => x.escalated_to_task_id,
                        principalSchema: "approval",
                        principalTable: "approval_task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approval_delegation_delegate_user_id_status_valid_to",
                schema: "approval",
                table: "approval_delegation",
                columns: new[] { "delegate_user_id", "status", "valid_to" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_outcome_idempotency_key",
                schema: "approval",
                table: "approval_instance",
                column: "outcome_idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_previous_instance_id",
                schema: "approval",
                table: "approval_instance",
                column: "previous_instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_requested_by_user_id_requested_at_id",
                schema: "approval",
                table: "approval_instance",
                columns: new[] { "requested_by_user_id", "requested_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_instance_subject_revision",
                schema: "approval",
                table: "approval_instance",
                columns: new[] { "subject_module", "subject_type", "subject_id", "subject_revision_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_approval_delegation_id",
                schema: "approval",
                table: "approval_task",
                column: "approval_delegation_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_assigned_role_id_status_due_at_id",
                schema: "approval",
                table: "approval_task",
                columns: new[] { "assigned_role_id", "status", "due_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_assigned_user_id_status_due_at_id",
                schema: "approval",
                table: "approval_task",
                columns: new[] { "assigned_user_id", "status", "due_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_escalated_to_task_id",
                schema: "approval",
                table: "approval_task",
                column: "escalated_to_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_approval_task_instance_stage_role",
                schema: "approval",
                table: "approval_task",
                columns: new[] { "approval_instance_id", "sequence_no", "assigned_role_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_task",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_delegation",
                schema: "approval");

            migrationBuilder.DropTable(
                name: "approval_instance",
                schema: "approval");
        }
    }
}
