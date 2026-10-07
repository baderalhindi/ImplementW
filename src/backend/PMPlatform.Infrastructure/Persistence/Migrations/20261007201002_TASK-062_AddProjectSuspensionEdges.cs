using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-062, 3 of 4: WF-09's two edges of the project lifecycle, ACTIVE → SUSPENDED and SUSPENDED → ACTIVE, admitted by replacing
    /// <c>project.guard_project()</c> as TASK-041 provides (project-registration.md §6). Nothing else of the guard changes: the Formal Project
    /// ID, the intake marker and the revision keep their rules, and <c>activated_at</c> keeps the project's first activation through a
    /// resumption. That a project moves along these edges only with its active suspension is migration 4's.
    /// </summary>
    public partial class TASK062_AddProjectSuspensionEdges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

                    IF NEW.lifecycle_state IS DISTINCT FROM OLD.lifecycle_state
                       AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                           ('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'DRAFT'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'),
                           ('UNDER_REVIEW', 'APPROVED_PLANNED'), ('RETURNED', 'SUBMITTED'), ('APPROVED_PLANNED', 'ACTIVE'),
                           ('ACTIVE', 'SUSPENDED'), ('SUSPENDED', 'ACTIVE')) THEN
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
                """);
        }
    }
}
