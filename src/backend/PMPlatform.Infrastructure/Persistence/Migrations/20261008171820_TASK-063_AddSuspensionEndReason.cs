using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-063, 1 of 5: WF-10's terminal path closes a SUSPENDED project without resuming it (WF-10 §9), so a suspension period may now end
    /// as PROJECT_CLOSED, with no resumption request. <c>active_suspension.end_reason</c> records why a period ended — RESUMED or
    /// PROJECT_CLOSED — and the periods ended before it are given RESUMED, which is how each of them ended. The ended-period CHECK is
    /// replaced by two that accept every row and every write the previous release makes (which records no reason), and the commit-time
    /// check of <c>TASK-062_GuardSuspensionHistory</c> also requires a period ended as PROJECT_CLOSED to be its CLOSED project's.
    /// </summary>
    public partial class TASK063_AddSuspensionEndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replaced below by two CHECKs that accept every row the previous one did and every write the previous release makes: not a contraction.
            migrationBuilder.Sql("ALTER TABLE suspension.active_suspension DROP CONSTRAINT ck_active_suspension_ended; -- EXPAND-THEN-CONTRACT-REVIEWED: relaxed, re-added in this migration (TASK-063)");

            migrationBuilder.AddColumn<string>(
                name: "end_reason",
                schema: "suspension",
                table: "active_suspension",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Every period ended so far ended by a resumption. An ended period changes no more (TASK-062's guard), so the guard is set aside
            // for this one statement, which writes only the new column.
            migrationBuilder.Sql("""
                ALTER TABLE suspension.active_suspension DISABLE TRIGGER guard_active_suspension;
                UPDATE suspension.active_suspension SET end_reason = 'RESUMED' WHERE ended_at IS NOT NULL AND end_reason IS NULL;
                ALTER TABLE suspension.active_suspension ENABLE TRIGGER guard_active_suspension;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_active_suspension_end_reason",
                schema: "suspension",
                table: "active_suspension",
                sql: "\"end_reason\" IN ('RESUMED', 'PROJECT_CLOSED')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_active_suspension_end_reason_request",
                schema: "suspension",
                table: "active_suspension",
                sql: "end_reason IS NULL OR (end_reason = 'RESUMED') = (resumption_request_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_active_suspension_ended",
                schema: "suspension",
                table: "active_suspension",
                sql: "(ended_at IS NULL OR ended_at >= started_at) AND (ended_at IS NULL) = (resumption_request_id IS NULL AND end_reason IS NULL)");

            migrationBuilder.Sql("""
                -- At commit: a period's requests are EFFECTED requests of its project — the suspension that opened it, the resumption that
                -- ended it — and a period ended as PROJECT_CLOSED is of a CLOSED project.
                CREATE OR REPLACE FUNCTION suspension.check_active_suspension() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM suspension.suspension_request r WHERE r.id = NEW.suspension_request_id AND r.project_id = NEW.project_id
                                   AND r.request_type = 'SUSPEND' AND r.status = 'EFFECTED')
                       OR (NEW.resumption_request_id IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM suspension.suspension_request r WHERE r.id = NEW.resumption_request_id AND r.project_id = NEW.project_id
                                           AND r.request_type = 'RESUME' AND r.status = 'EFFECTED')) THEN
                        RAISE EXCEPTION 'active_suspension %: it is opened by an effected suspension request of its project, and ended by an effected resumption request of it', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.end_reason = 'PROJECT_CLOSED'
                       AND NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id AND p.lifecycle_state = 'CLOSED') THEN
                        RAISE EXCEPTION 'active_suspension %: it ends as PROJECT_CLOSED only with its project closed', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    PERFORM suspension.check_project_suspension(NEW.project_id);
                    RETURN NULL;
                END;
                $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION suspension.check_active_suspension() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM suspension.suspension_request r WHERE r.id = NEW.suspension_request_id AND r.project_id = NEW.project_id
                                   AND r.request_type = 'SUSPEND' AND r.status = 'EFFECTED')
                       OR (NEW.resumption_request_id IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM suspension.suspension_request r WHERE r.id = NEW.resumption_request_id AND r.project_id = NEW.project_id
                                           AND r.request_type = 'RESUME' AND r.status = 'EFFECTED')) THEN
                        RAISE EXCEPTION 'active_suspension %: it is opened by an effected suspension request of its project, and ended by an effected resumption request of it', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    PERFORM suspension.check_project_suspension(NEW.project_id);
                    RETURN NULL;
                END;
                $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_active_suspension_end_reason",
                schema: "suspension",
                table: "active_suspension");

            migrationBuilder.DropCheckConstraint(
                name: "ck_active_suspension_end_reason_request",
                schema: "suspension",
                table: "active_suspension");

            migrationBuilder.DropCheckConstraint(
                name: "ck_active_suspension_ended",
                schema: "suspension",
                table: "active_suspension");

            migrationBuilder.DropColumn(
                name: "end_reason",
                schema: "suspension",
                table: "active_suspension");

            migrationBuilder.AddCheckConstraint(
                name: "ck_active_suspension_ended",
                schema: "suspension",
                table: "active_suspension",
                sql: "(ended_at IS NULL) = (resumption_request_id IS NULL) AND (ended_at IS NULL OR ended_at >= started_at)");
        }
    }
}
