using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-041: the WF-01 lifecycle is fixed in the database as well as in the application, whoever writes to the row.
    /// A project's state changes only along the seven edges TASK-041 builds — so nothing reaches APPROVED_PLANNED but from
    /// UNDER_REVIEW (no fast-track, PTBC-002) and nothing reaches ACTIVE but by the activation command, which stamps
    /// <c>activated_at</c> in the same write. The Formal Project ID is issued once, on approval, from
    /// <c>project.formal_project_id_seq</c>, and never changes; the ADR-014 intake marker is set once and never changes; the
    /// revision moves only on resubmission after RETURNED, by one; only a DRAFT is deleted (HARD_DRAFT). TASK-062 and
    /// TASK-063 add their edges by replacing <c>project.guard_project()</c> in a migration of their own. Also maps
    /// <c>xmin</c> as the project's concurrency token (ERD D-16), which emits no DDL.
    /// </summary>
    public partial class TASK041_GuardProjectLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "project",
                table: "project",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.Sql("""
                CREATE SEQUENCE project.formal_project_id_seq AS bigint START WITH 1 NO CYCLE;

                CREATE FUNCTION project.guard_project() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.lifecycle_state <> 'DRAFT' THEN
                            RAISE EXCEPTION 'project % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.lifecycle_state
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF NEW.lifecycle_state IS DISTINCT FROM OLD.lifecycle_state
                       AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                           ('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'DRAFT'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'),
                           ('UNDER_REVIEW', 'APPROVED_PLANNED'), ('RETURNED', 'SUBMITTED'), ('APPROVED_PLANNED', 'ACTIVE')) THEN
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

                    IF (OLD.lifecycle_state = 'RETURNED' AND NEW.lifecycle_state = 'SUBMITTED')
                       IS DISTINCT FROM (NEW.revision_no = OLD.revision_no + 1)
                       OR NEW.revision_no NOT IN (OLD.revision_no, OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'project %: the revision moves only on resubmission after RETURNED, by one', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project
                    BEFORE UPDATE OR DELETE ON project.project
                    FOR EACH ROW EXECUTE FUNCTION project.guard_project();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_project ON project.project;
                DROP FUNCTION project.guard_project();
                DROP SEQUENCE project.formal_project_id_seq;
                """);

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "project",
                table: "project");
        }
    }
}
