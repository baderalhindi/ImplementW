using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-063, 5 of 5: WF-10's history and its acceptance criteria held in the database, whoever writes. A case is born DRAFT at revision 1
    /// for a project its kind admits — a completion case for an ACTIVE project, a closure case for a COMPLETED one after its effected
    /// completion, or a SUSPENDED one on the terminal path; it keeps its project, requester and path; moves only along CloseoutWorkflow's
    /// edges; raises its revision by one exactly when a RETURNED case is resubmitted; changes its fields only while DRAFT or RETURNED; is
    /// APPROVED only by its revision's approved WF-11 run; changes no more once final; and only a DRAFT is deleted. A readiness record is
    /// never changed or deleted. An obligation is born OPEN for its case's project, moves only along its edges, changes no more once
    /// settled, and is never deleted. At commit (deferred constraint triggers): a project is COMPLETED only with its effected completion case
    /// and CLOSED only with its effected closure case of the path it was on, and an effected case's project is in the state it took it to.
    /// So no progress figure, task or date completes a project — acceptance criterion 1 — and no approval alone moves it. No table is
    /// truncated. Reads <c>project.project</c> and <c>approval.approval_instance</c>, and adds one trigger to <c>project.project</c>.
    /// </summary>
    public partial class TASK063_GuardCloseoutHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION closure.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'closure.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON closure.completion_case FOR EACH STATEMENT EXECUTE FUNCTION closure.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON closure.closure_case FOR EACH STATEMENT EXECUTE FUNCTION closure.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON closure.readiness_check FOR EACH STATEMENT EXECUTE FUNCTION closure.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON closure.post_project_obligation FOR EACH STATEMENT EXECUTE FUNCTION closure.refuse_truncation();

                -- A completion or closure case: born DRAFT at revision 1 for a project its kind admits; keeps its project, requester and path;
                -- moves only along CloseoutWorkflow's edges; raises its revision by one exactly when a RETURNED case is resubmitted; changes its
                -- fields only while DRAFT or RETURNED; is APPROVED only by its revision's approved WF-11 run; changes no more once final.
                CREATE FUNCTION closure.guard_case() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    kind text := CASE TG_TABLE_NAME WHEN 'completion_case' THEN 'CompletionCase' ELSE 'ClosureCase' END;
                    lifecycle text[] := ARRAY['status', 'revision_no', 'submitted_at', 'effected_at', 'updated_at', 'updated_by'];
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION '%.% % is %: only a DRAFT never submitted is deleted (HARD_DRAFT)', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' OR NEW.revision_no <> 1 OR NEW.submitted_at IS NOT NULL OR NEW.effected_at IS NOT NULL THEN
                            RAISE EXCEPTION '%.% %: a case is born DRAFT, at revision 1, never submitted', TG_TABLE_SCHEMA, TG_TABLE_NAME, NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_closure_case_born';
                        END IF;

                        IF kind = 'CompletionCase' THEN
                            IF NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id AND p.lifecycle_state = 'ACTIVE') THEN
                                RAISE EXCEPTION 'completion_case %: a completion case is raised for an ACTIVE project', NEW.id
                                    USING ERRCODE = 'restrict_violation';
                            END IF;
                        ELSIF (to_jsonb(NEW) ->> 'completion_case_id') IS NULL THEN
                            IF NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id AND p.lifecycle_state = 'SUSPENDED') THEN
                                RAISE EXCEPTION 'closure_case %: a closure case without a completion is raised for a SUSPENDED project (the terminal path)', NEW.id
                                    USING ERRCODE = 'restrict_violation';
                            END IF;
                        ELSIF NOT EXISTS (SELECT 1 FROM project.project p JOIN closure.completion_case c ON c.project_id = p.id
                                          WHERE p.id = NEW.project_id AND p.lifecycle_state = 'COMPLETED'
                                            AND c.id = (to_jsonb(NEW) ->> 'completion_case_id')::uuid AND c.status = 'EFFECTED') THEN
                            RAISE EXCEPTION 'closure_case %: a closure case follows the effected completion case of its COMPLETED project', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (to_jsonb(NEW) -> 'id', to_jsonb(NEW) -> 'project_id', to_jsonb(NEW) -> 'requested_by_user_id', to_jsonb(NEW) -> 'completion_case_id',
                        to_jsonb(NEW) -> 'created_at', to_jsonb(NEW) -> 'created_by')
                       IS DISTINCT FROM (to_jsonb(OLD) -> 'id', to_jsonb(OLD) -> 'project_id', to_jsonb(OLD) -> 'requested_by_user_id', to_jsonb(OLD) -> 'completion_case_id',
                        to_jsonb(OLD) -> 'created_at', to_jsonb(OLD) -> 'created_by') THEN
                        RAISE EXCEPTION '%.% %: its project, requester and path never change', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('REJECTED', 'WITHDRAWN', 'EFFECTED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION '%.% % is % and changes no more', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'),
                        ('UNDER_REVIEW', 'APPROVED'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'REJECTED'), ('UNDER_REVIEW', 'WITHDRAWN'),
                        ('DRAFT', 'WITHDRAWN'), ('SUBMITTED', 'WITHDRAWN'), ('RETURNED', 'WITHDRAWN'), ('APPROVED', 'EFFECTED')) THEN
                        RAISE EXCEPTION '%.% %: % to % is not a step of the case''s state machine', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no <> OLD.revision_no + (CASE WHEN OLD.status = 'RETURNED' AND NEW.status = 'SUBMITTED' THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION '%.% %: the revision rises by one when a returned case is resubmitted, and only then', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status NOT IN ('DRAFT', 'RETURNED') AND (to_jsonb(NEW) - lifecycle) IS DISTINCT FROM (to_jsonb(OLD) - lifecycle) THEN
                        RAISE EXCEPTION '%.% % is %: its fields are those it was submitted with', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.submitted_at IS DISTINCT FROM OLD.submitted_at AND NOT (NEW.status = 'SUBMITTED' AND OLD.status IN ('DRAFT', 'RETURNED')) THEN
                        RAISE EXCEPTION '%.% %: it is submitted only by its submission', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'UNDER_REVIEW' AND NEW.status = 'APPROVED'
                       AND NOT EXISTS (SELECT 1 FROM approval.approval_instance i
                                       WHERE i.subject_module = 'Closure' AND i.subject_type = kind AND i.subject_id = NEW.id
                                         AND i.subject_revision_no = NEW.revision_no AND i.status = 'APPROVED') THEN
                        RAISE EXCEPTION '%.% %: it is approved by the approved WF-11 run of revision %', TG_TABLE_SCHEMA, TG_TABLE_NAME, OLD.id, NEW.revision_no
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_case BEFORE INSERT OR UPDATE OR DELETE ON closure.completion_case FOR EACH ROW EXECUTE FUNCTION closure.guard_case();
                CREATE TRIGGER guard_case BEFORE INSERT OR UPDATE OR DELETE ON closure.closure_case FOR EACH ROW EXECUTE FUNCTION closure.guard_case();

                -- A readiness record is APPEND_ONLY: an evaluation or a waiver, as recorded, never changed or removed. A waiver is of a case's
                -- failed criterion; who accepted it, and why, are on the row (CHECK ck_readiness_check_waiver).
                CREATE FUNCTION closure.guard_readiness_check() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'readiness_check %: a readiness record is never changed or deleted (APPEND_ONLY)', OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER guard_readiness_check BEFORE UPDATE OR DELETE ON closure.readiness_check FOR EACH ROW EXECUTE FUNCTION closure.guard_readiness_check();

                -- A post-project obligation: born OPEN for its case's project; keeps its project and case; OPEN → IN_PROGRESS, and either to
                -- SATISFIED, WAIVED or CANCELLED, which are final; never deleted (RETAIN).
                CREATE FUNCTION closure.guard_post_project_obligation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'post_project_obligation % is retained and never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'OPEN' THEN
                            RAISE EXCEPTION 'post_project_obligation %: an obligation is born OPEN', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_post_project_obligation_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM closure.completion_case c WHERE c.id = NEW.completion_case_id AND c.project_id = NEW.project_id)
                           AND NOT EXISTS (SELECT 1 FROM closure.closure_case c WHERE c.id = NEW.closure_case_id AND c.project_id = NEW.project_id) THEN
                            RAISE EXCEPTION 'post_project_obligation %: it is of its case''s project', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.completion_case_id, NEW.closure_case_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.completion_case_id, OLD.closure_case_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'post_project_obligation %: its project and case never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('SATISFIED', 'WAIVED', 'CANCELLED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'post_project_obligation % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('OPEN', 'IN_PROGRESS'), ('OPEN', 'SATISFIED'), ('OPEN', 'WAIVED'), ('OPEN', 'CANCELLED'),
                        ('IN_PROGRESS', 'SATISFIED'), ('IN_PROGRESS', 'WAIVED'), ('IN_PROGRESS', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'post_project_obligation %: % to % is not a step of an obligation', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_post_project_obligation BEFORE INSERT OR UPDATE OR DELETE ON closure.post_project_obligation
                    FOR EACH ROW EXECUTE FUNCTION closure.guard_post_project_obligation();

                -- At commit: a project enters COMPLETED only with its effected completion case, and CLOSED only with its effected closure case —
                -- of the terminal path from SUSPENDED, of the normal path from COMPLETED. So no progress figure, task or date completes a project,
                -- no approval alone moves it, and no direct write does either.
                CREATE FUNCTION closure.check_project_closeout() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.lifecycle_state = 'COMPLETED'
                       AND NOT EXISTS (SELECT 1 FROM closure.completion_case c WHERE c.project_id = NEW.id AND c.status = 'EFFECTED') THEN
                        RAISE EXCEPTION 'project %: it is COMPLETED only by its effected completion case', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state = 'CLOSED'
                       AND NOT EXISTS (SELECT 1 FROM closure.closure_case c WHERE c.project_id = NEW.id AND c.status = 'EFFECTED'
                                       AND (c.completion_case_id IS NULL) = (OLD.lifecycle_state = 'SUSPENDED')) THEN
                        RAISE EXCEPTION 'project %: it is CLOSED only by its effected closure case of the path it was on', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_project_closeout AFTER UPDATE OF lifecycle_state ON project.project
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    WHEN (OLD.lifecycle_state IS DISTINCT FROM NEW.lifecycle_state AND NEW.lifecycle_state IN ('COMPLETED', 'CLOSED'))
                    EXECUTE FUNCTION closure.check_project_closeout();

                -- At commit: a case is EFFECTED with its project's transition — a completion case's project COMPLETED, a closure case's CLOSED.
                CREATE FUNCTION closure.check_effected_case() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id
                                   AND p.lifecycle_state = CASE TG_TABLE_NAME WHEN 'completion_case' THEN 'COMPLETED' ELSE 'CLOSED' END) THEN
                        RAISE EXCEPTION '%.% %: it is effected with its project''s transition', TG_TABLE_SCHEMA, TG_TABLE_NAME, NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_effected_case AFTER UPDATE OF status ON closure.completion_case
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    WHEN (NEW.status = 'EFFECTED' AND OLD.status <> 'EFFECTED')
                    EXECUTE FUNCTION closure.check_effected_case();
                CREATE CONSTRAINT TRIGGER check_effected_case AFTER UPDATE OF status ON closure.closure_case
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    WHEN (NEW.status = 'EFFECTED' AND OLD.status <> 'EFFECTED')
                    EXECUTE FUNCTION closure.check_effected_case();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER check_effected_case ON closure.closure_case;
                DROP TRIGGER check_effected_case ON closure.completion_case;
                DROP FUNCTION closure.check_effected_case();
                DROP TRIGGER check_project_closeout ON project.project;
                DROP FUNCTION closure.check_project_closeout();
                DROP TRIGGER guard_post_project_obligation ON closure.post_project_obligation;
                DROP FUNCTION closure.guard_post_project_obligation();
                DROP TRIGGER guard_readiness_check ON closure.readiness_check;
                DROP FUNCTION closure.guard_readiness_check();
                DROP TRIGGER guard_case ON closure.closure_case;
                DROP TRIGGER guard_case ON closure.completion_case;
                DROP FUNCTION closure.guard_case();
                DROP TRIGGER refuse_truncation ON closure.post_project_obligation;
                DROP TRIGGER refuse_truncation ON closure.readiness_check;
                DROP TRIGGER refuse_truncation ON closure.closure_case;
                DROP TRIGGER refuse_truncation ON closure.completion_case;
                DROP FUNCTION closure.refuse_truncation();
                """);
        }
    }
}
