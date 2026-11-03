using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TASK111_AddProjectFollowUpAndImportBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "current_obstacle",
                schema: "project",
                table: "project",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "current_obstacle_lang",
                schema: "project",
                table: "project",
                type: "char(2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_actual_percent",
                schema: "project",
                table: "project",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_actual_spend_percent",
                schema: "project",
                table: "project",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "declared_as_of",
                schema: "project",
                table: "project",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_planned_percent",
                schema: "project",
                table: "project",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "declared_planned_spend_percent",
                schema: "project",
                table: "project",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "declared_source",
                schema: "project",
                table: "project",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "entity_update_compliant",
                schema: "project",
                table: "project",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "follow_up_owner_user_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "funding_source",
                schema: "project",
                table: "project",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_reference",
                schema: "project",
                table: "project",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_action",
                schema: "project",
                table: "project",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "next_action_due_date",
                schema: "project",
                table: "project",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_action_lang",
                schema: "project",
                table: "project",
                type: "char(2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "next_action_owner",
                schema: "project",
                table: "project",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "priority_item_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "project_import_batch_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "strategic_initiative_item_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "strategic_portfolio_item_id",
                schema: "project",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_follow_up_note",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_lang = table.Column<string>(type: "char(2)", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_follow_up_note", x => x.id);
                    table.CheckConstraint("ck_project_follow_up_note_append_only", "updated_at = created_at AND updated_by = created_by");
                    table.CheckConstraint("ck_project_follow_up_note_note_lang", "\"note_lang\" IN ('ar', 'en')");
                    table.ForeignKey(
                        name: "fk_project_follow_up_note_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "project",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_import_batch",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_no = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_sha256 = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: false),
                    file_content = table.Column<byte[]>(type: "bytea", nullable: false),
                    plan = table.Column<string>(type: "jsonb", nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    create_count = table.Column<int>(type: "integer", nullable: false),
                    update_count = table.Column<int>(type: "integer", nullable: false),
                    skip_count = table.Column<int>(type: "integer", nullable: false),
                    warning_count = table.Column<int>(type: "integer", nullable: false),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rolled_back_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rolled_back_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    return_reason_lang = table.Column<string>(type: "char(2)", nullable: true),
                    return_reason = table.Column<string>(type: "text", nullable: true),
                    submission_note_lang = table.Column<string>(type: "char(2)", nullable: true),
                    submission_note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_import_batch", x => x.id);
                    table.CheckConstraint("ck_project_import_batch_decided_by", "decided_by_user_id IS NULL OR decided_by_user_id <> created_by");
                    table.CheckConstraint("ck_project_import_batch_decision", "(status = 'SUBMITTED') = (decided_at IS NULL) AND (status = 'RETURNED') = (return_reason IS NOT NULL)");
                    table.CheckConstraint("ck_project_import_batch_return_reason_lang", "\"return_reason_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_import_batch_return_reason_pair", "(\"return_reason\" IS NULL) = (\"return_reason_lang\" IS NULL)");
                    table.CheckConstraint("ck_project_import_batch_status", "\"status\" IN ('SUBMITTED', 'APPROVED', 'RETURNED', 'ROLLED_BACK')");
                    table.CheckConstraint("ck_project_import_batch_submission_note_lang", "\"submission_note_lang\" IN ('ar', 'en')");
                    table.CheckConstraint("ck_project_import_batch_submission_note_pair", "(\"submission_note\" IS NULL) = (\"submission_note_lang\" IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "project_import_name_mapping",
                schema: "project",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_import_name_mapping", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project",
                column: "legacy_reference",
                unique: true,
                filter: "legacy_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_project_import_batch_id",
                schema: "project",
                table: "project",
                column: "project_import_batch_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_current_obstacle_lang",
                schema: "project",
                table: "project",
                sql: "\"current_obstacle_lang\" IN ('ar', 'en')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_current_obstacle_pair",
                schema: "project",
                table: "project",
                sql: "(\"current_obstacle\" IS NULL) = (\"current_obstacle_lang\" IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_percent_range",
                schema: "project",
                table: "project",
                sql: "(declared_planned_percent IS NULL OR declared_planned_percent BETWEEN 0 AND 100) AND (declared_actual_percent IS NULL OR declared_actual_percent BETWEEN 0 AND 100) AND (declared_planned_spend_percent IS NULL OR declared_planned_spend_percent BETWEEN 0 AND 100) AND (declared_actual_spend_percent IS NULL OR declared_actual_spend_percent BETWEEN 0 AND 100)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project",
                sql: "\"declared_source\" IN ('IMPORT', 'MANUAL')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_funding_source",
                schema: "project",
                table: "project",
                sql: "\"funding_source\" IN ('STRATEGY_BUDGET_FULL', 'STRATEGY_BUDGET_PARTIAL', 'NOT_STRATEGY_BUDGET')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_next_action_lang",
                schema: "project",
                table: "project",
                sql: "\"next_action_lang\" IN ('ar', 'en')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_project_next_action_pair",
                schema: "project",
                table: "project",
                sql: "(\"next_action\" IS NULL) = (\"next_action_lang\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_project_follow_up_note_project_id_created_at_id",
                schema: "project",
                table: "project_follow_up_note",
                columns: new[] { "project_id", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_import_batch_batch_no",
                schema: "project",
                table: "project_import_batch",
                column: "batch_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_import_batch_created_at_id",
                schema: "project",
                table: "project_import_batch",
                columns: new[] { "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_import_batch_status_created_at_id",
                schema: "project",
                table: "project_import_batch",
                columns: new[] { "status", "created_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_project_import_name_mapping_dimension_source_key",
                schema: "project",
                table: "project_import_name_mapping",
                columns: new[] { "dimension", "source_key" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_project_project_import_batch_project_import_batch_id",
                schema: "project",
                table: "project",
                column: "project_import_batch_id",
                principalSchema: "project",
                principalTable: "project_import_batch",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // The project guard (TASK-041, TASK-063) lets a rolled-back import batch remove its projects.
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION project.guard_project() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        -- The 2 Nov 2026 review (R3-4): an import batch rolled back removes the projects it created, whatever their state.
                        IF OLD.lifecycle_state <> 'DRAFT' AND NOT EXISTS (
                            SELECT 1 FROM project.project_import_batch b
                            WHERE b.id = OLD.project_import_batch_id AND b.status = 'ROLLED_BACK') THEN
                            RAISE EXCEPTION 'project % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.lifecycle_state
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF OLD.lifecycle_state = 'CLOSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'project % is CLOSED: terminal and read-only', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state IS DISTINCT FROM OLD.lifecycle_state
                       AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                           ('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'DRAFT'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'),
                           ('UNDER_REVIEW', 'APPROVED_PLANNED'), ('RETURNED', 'SUBMITTED'), ('APPROVED_PLANNED', 'ACTIVE'),
                           ('ACTIVE', 'SUSPENDED'), ('SUSPENDED', 'ACTIVE'),
                           ('ACTIVE', 'COMPLETED'), ('COMPLETED', 'CLOSED'), ('SUSPENDED', 'CLOSED')) THEN
                        RAISE EXCEPTION 'project %: % to % is not a lifecycle transition', OLD.id, OLD.lifecycle_state, NEW.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.formal_project_id IS DISTINCT FROM OLD.formal_project_id
                       AND NOT (OLD.formal_project_id IS NULL AND OLD.lifecycle_state = 'UNDER_REVIEW' AND NEW.lifecycle_state = 'APPROVED_PLANNED') THEN
                        RAISE EXCEPTION 'project %: the Formal Project ID is issued once, on approval, and never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.legacy_intake_date IS NOT NULL AND NEW.legacy_intake_date IS DISTINCT FROM OLD.legacy_intake_date THEN
                        RAISE EXCEPTION 'project %: the intake marker is set once and never changes (ADR-014)', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state = 'APPROVED_PLANNED' AND NEW.lifecycle_state = 'ACTIVE')
                       IS DISTINCT FROM (OLD.activated_at IS NULL AND NEW.activated_at IS NOT NULL)
                       OR (OLD.activated_at IS NOT NULL AND NEW.activated_at IS DISTINCT FROM OLD.activated_at) THEN
                        RAISE EXCEPTION 'project %: activated_at is stamped by the activation command, once', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state <> 'CLOSED' AND NEW.lifecycle_state = 'CLOSED')
                       IS DISTINCT FROM (OLD.closed_at IS NULL AND NEW.closed_at IS NOT NULL)
                       OR (OLD.closed_at IS NOT NULL AND NEW.closed_at IS DISTINCT FROM OLD.closed_at) THEN
                        RAISE EXCEPTION 'project %: closed_at is stamped by the closure, once', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state = 'RETURNED' AND NEW.lifecycle_state = 'SUBMITTED')
                       IS DISTINCT FROM (NEW.revision_no = OLD.revision_no + 1)
                       OR NEW.revision_no NOT IN (OLD.revision_no, OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'project %: the revision moves only on resubmission after RETURNED, by one', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION project.guard_project() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.lifecycle_state <> 'DRAFT' THEN
                            RAISE EXCEPTION 'project % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.lifecycle_state
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF OLD.lifecycle_state = 'CLOSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'project % is CLOSED: terminal and read-only', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state IS DISTINCT FROM OLD.lifecycle_state
                       AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                           ('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'DRAFT'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'),
                           ('UNDER_REVIEW', 'APPROVED_PLANNED'), ('RETURNED', 'SUBMITTED'), ('APPROVED_PLANNED', 'ACTIVE'),
                           ('ACTIVE', 'SUSPENDED'), ('SUSPENDED', 'ACTIVE'),
                           ('ACTIVE', 'COMPLETED'), ('COMPLETED', 'CLOSED'), ('SUSPENDED', 'CLOSED')) THEN
                        RAISE EXCEPTION 'project %: % to % is not a lifecycle transition', OLD.id, OLD.lifecycle_state, NEW.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.formal_project_id IS DISTINCT FROM OLD.formal_project_id
                       AND NOT (OLD.formal_project_id IS NULL AND OLD.lifecycle_state = 'UNDER_REVIEW' AND NEW.lifecycle_state = 'APPROVED_PLANNED') THEN
                        RAISE EXCEPTION 'project %: the Formal Project ID is issued once, on approval, and never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.legacy_intake_date IS NOT NULL AND NEW.legacy_intake_date IS DISTINCT FROM OLD.legacy_intake_date THEN
                        RAISE EXCEPTION 'project %: the intake marker is set once and never changes (ADR-014)', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state = 'APPROVED_PLANNED' AND NEW.lifecycle_state = 'ACTIVE')
                       IS DISTINCT FROM (OLD.activated_at IS NULL AND NEW.activated_at IS NOT NULL)
                       OR (OLD.activated_at IS NOT NULL AND NEW.activated_at IS DISTINCT FROM OLD.activated_at) THEN
                        RAISE EXCEPTION 'project %: activated_at is stamped by the activation command, once', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state <> 'CLOSED' AND NEW.lifecycle_state = 'CLOSED')
                       IS DISTINCT FROM (OLD.closed_at IS NULL AND NEW.closed_at IS NOT NULL)
                       OR (OLD.closed_at IS NOT NULL AND NEW.closed_at IS DISTINCT FROM OLD.closed_at) THEN
                        RAISE EXCEPTION 'project %: closed_at is stamped by the closure, once', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (OLD.lifecycle_state = 'RETURNED' AND NEW.lifecycle_state = 'SUBMITTED')
                       IS DISTINCT FROM (NEW.revision_no = OLD.revision_no + 1)
                       OR NEW.revision_no NOT IN (OLD.revision_no, OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'project %: the revision moves only on resubmission after RETURNED, by one', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_project_project_import_batch_project_import_batch_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropTable(
                name: "project_follow_up_note",
                schema: "project");

            migrationBuilder.DropTable(
                name: "project_import_batch",
                schema: "project");

            migrationBuilder.DropTable(
                name: "project_import_name_mapping",
                schema: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_legacy_reference",
                schema: "project",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_project_import_batch_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_current_obstacle_lang",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_current_obstacle_pair",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_declared_percent_range",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_declared_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_funding_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_next_action_lang",
                schema: "project",
                table: "project");

            migrationBuilder.DropCheckConstraint(
                name: "ck_project_next_action_pair",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "current_obstacle",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "current_obstacle_lang",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_actual_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_actual_spend_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_as_of",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_planned_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_planned_spend_percent",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "declared_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "entity_update_compliant",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "follow_up_owner_user_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "funding_source",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "legacy_reference",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_action",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_action_due_date",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_action_lang",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_action_owner",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "priority_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "project_import_batch_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "strategic_initiative_item_id",
                schema: "project",
                table: "project");

            migrationBuilder.DropColumn(
                name: "strategic_portfolio_item_id",
                schema: "project",
                table: "project");
        }
    }
}
