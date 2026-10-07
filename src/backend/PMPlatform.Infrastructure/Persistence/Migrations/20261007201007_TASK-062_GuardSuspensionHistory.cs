using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-062, 4 of 4: WF-09's history and its acceptance criteria held in the database, whoever writes. A request is born DRAFT at
    /// revision 1, never submitted, for a project its type admits — a suspension of an ACTIVE project, a resumption of a SUSPENDED one —
    /// so a second suspension of a suspended project is refused here too. It keeps its project, type and requester; it moves only along the
    /// edges of <c>SuspensionWorkflow</c>; its revision rises by one exactly when a RETURNED request is resubmitted; its fields change only
    /// while it is with its requester; it is APPROVED only by its revision's approved WF-11 run, and EFFECTED only from APPROVED; once
    /// REJECTED, WITHDRAWN or EFFECTED nothing changes; only a DRAFT is deleted. An active suspension is born open for its project by a
    /// suspension request of that project, is ended once, by a resumption request of that project, and is never deleted. At commit
    /// (deferred constraint triggers, so the order of one transaction's writes does not matter) three things agree: a project is SUSPENDED
    /// exactly while it has an open active suspension; an EFFECTED request has the period it opened or ended; and every period's requests
    /// are EFFECTED. So approval alone moves no project, and no project moves but with its suspension record. No table is truncated. Reads
    /// <c>project.project</c> and <c>approval.approval_instance</c>, and adds one trigger to <c>project.project</c>.
    /// </summary>
    public partial class TASK062_GuardSuspensionHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION suspension.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'suspension.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON suspension.suspension_request FOR EACH STATEMENT EXECUTE FUNCTION suspension.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON suspension.active_suspension FOR EACH STATEMENT EXECUTE FUNCTION suspension.refuse_truncation();

                CREATE FUNCTION suspension.guard_suspension_request() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'suspension_request % is %: only a DRAFT never submitted is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' OR NEW.revision_no <> 1 OR NEW.submitted_at IS NOT NULL OR NEW.effected_at IS NOT NULL THEN
                            RAISE EXCEPTION 'suspension_request %: a request is born DRAFT, at revision 1, never submitted', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_suspension_request_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id
                                       AND p.lifecycle_state = CASE NEW.request_type WHEN 'SUSPEND' THEN 'ACTIVE' ELSE 'SUSPENDED' END) THEN
                            RAISE EXCEPTION 'suspension_request %: a suspension is raised for an ACTIVE project, a resumption for a SUSPENDED one', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.request_type, NEW.requested_by_user_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.request_type, OLD.requested_by_user_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'suspension_request %: its project, type and requester never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('REJECTED', 'WITHDRAWN', 'EFFECTED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'suspension_request % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'),
                        ('UNDER_REVIEW', 'APPROVED'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'REJECTED'), ('UNDER_REVIEW', 'WITHDRAWN'),
                        ('SUBMITTED', 'WITHDRAWN'), ('RETURNED', 'WITHDRAWN'), ('APPROVED', 'EFFECTED')) THEN
                        RAISE EXCEPTION 'suspension_request %: % to % is not a step of the request''s state machine', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no <> OLD.revision_no + (CASE WHEN OLD.status = 'RETURNED' AND NEW.status = 'SUBMITTED' THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION 'suspension_request %: the revision rises by one when a returned request is resubmitted, and only then', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status NOT IN ('DRAFT', 'RETURNED')
                       AND (NEW.reason, NEW.reason_lang, NEW.requested_effective_date, NEW.planned_resumption_date)
                           IS DISTINCT FROM (OLD.reason, OLD.reason_lang, OLD.requested_effective_date, OLD.planned_resumption_date) THEN
                        RAISE EXCEPTION 'suspension_request % is %: its fields are those it was submitted with', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.submitted_at IS DISTINCT FROM OLD.submitted_at AND NOT (NEW.status = 'SUBMITTED' AND OLD.status IN ('DRAFT', 'RETURNED')) THEN
                        RAISE EXCEPTION 'suspension_request %: it is submitted only by its submission', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'UNDER_REVIEW' AND NEW.status = 'APPROVED'
                       AND NOT EXISTS (SELECT 1 FROM approval.approval_instance i
                                       WHERE i.subject_module = 'Suspension' AND i.subject_id = NEW.id AND i.subject_revision_no = NEW.revision_no
                                         AND i.status = 'APPROVED') THEN
                        RAISE EXCEPTION 'suspension_request %: it is approved by the approved WF-11 run of revision %', OLD.id, NEW.revision_no
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_suspension_request BEFORE INSERT OR UPDATE OR DELETE ON suspension.suspension_request
                    FOR EACH ROW EXECUTE FUNCTION suspension.guard_suspension_request();

                CREATE FUNCTION suspension.guard_active_suspension() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'active_suspension % is retained and never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.ended_at IS NOT NULL OR NEW.resumption_request_id IS NOT NULL THEN
                            RAISE EXCEPTION 'active_suspension %: a suspension is born open', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_active_suspension_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.suspension_request_id, NEW.started_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.suspension_request_id, OLD.started_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'active_suspension %: its project, its suspension request and when it started never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.ended_at IS NOT NULL AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'active_suspension % has ended and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_active_suspension BEFORE INSERT OR UPDATE OR DELETE ON suspension.active_suspension
                    FOR EACH ROW EXECUTE FUNCTION suspension.guard_active_suspension();

                -- At commit: a project is SUSPENDED exactly while it has an open active suspension.
                CREATE FUNCTION suspension.check_project_suspension(checked_project_id uuid) RETURNS void
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (SELECT p.lifecycle_state = 'SUSPENDED' FROM project.project p WHERE p.id = checked_project_id)
                       IS DISTINCT FROM EXISTS (SELECT 1 FROM suspension.active_suspension s WHERE s.project_id = checked_project_id AND s.ended_at IS NULL) THEN
                        RAISE EXCEPTION 'project %: a project is SUSPENDED exactly while it has an open active suspension', checked_project_id
                            USING ERRCODE = 'restrict_violation';
                    END IF;
                END;
                $$;

                CREATE FUNCTION suspension.check_project_lifecycle() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM suspension.check_project_suspension(NEW.id);
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_project_suspension AFTER UPDATE OF lifecycle_state ON project.project
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    WHEN (OLD.lifecycle_state IS DISTINCT FROM NEW.lifecycle_state AND 'SUSPENDED' IN (OLD.lifecycle_state, NEW.lifecycle_state))
                    EXECUTE FUNCTION suspension.check_project_lifecycle();

                -- At commit: a period's requests are EFFECTED requests of its project — the suspension that opened it, the resumption that ended it.
                CREATE FUNCTION suspension.check_active_suspension() RETURNS trigger
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

                CREATE CONSTRAINT TRIGGER check_active_suspension AFTER INSERT OR UPDATE ON suspension.active_suspension
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION suspension.check_active_suspension();

                -- At commit: an EFFECTED request has the period it opened or ended.
                CREATE FUNCTION suspension.check_effected_request() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM suspension.active_suspension s
                                   WHERE (NEW.request_type = 'SUSPEND' AND s.suspension_request_id = NEW.id)
                                      OR (NEW.request_type = 'RESUME' AND s.resumption_request_id = NEW.id)) THEN
                        RAISE EXCEPTION 'suspension_request %: it is effected with the suspension period it opens or ends', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_effected_request AFTER UPDATE OF status ON suspension.suspension_request
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW
                    WHEN (NEW.status = 'EFFECTED' AND OLD.status <> 'EFFECTED')
                    EXECUTE FUNCTION suspension.check_effected_request();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER check_effected_request ON suspension.suspension_request;
                DROP FUNCTION suspension.check_effected_request();
                DROP TRIGGER check_active_suspension ON suspension.active_suspension;
                DROP FUNCTION suspension.check_active_suspension();
                DROP TRIGGER check_project_suspension ON project.project;
                DROP FUNCTION suspension.check_project_lifecycle();
                DROP FUNCTION suspension.check_project_suspension(uuid);
                DROP TRIGGER guard_active_suspension ON suspension.active_suspension;
                DROP FUNCTION suspension.guard_active_suspension();
                DROP TRIGGER guard_suspension_request ON suspension.suspension_request;
                DROP FUNCTION suspension.guard_suspension_request();
                DROP TRIGGER refuse_truncation ON suspension.active_suspension;
                DROP TRIGGER refuse_truncation ON suspension.suspension_request;
                DROP FUNCTION suspension.refuse_truncation();
                """);
        }
    }
}
