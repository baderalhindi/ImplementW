using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-046, 4 of 4: the schedule's rules held in the database, whoever writes. A baseline is born DRAFT, or ACTIVE as an
    /// ADR-014 Declared Baseline; it moves only along the edges of <c>BaselineWorkflow</c>; an APPROVED baseline activates only
    /// with its copy of the schedule, which is written before it activates and never changed; once ACTIVE it changes only by
    /// being superseded by another baseline of its project, and SUPERSEDED, REJECTED and WITHDRAWN are final; only a DRAFT is
    /// deleted. With the partial unique index of 1 of 4, no interleaving of writers leaves two ACTIVE baselines. While a
    /// candidate is SUBMITTED to WF-11 the plan it will activate from is frozen. A dependency joins two activities of one
    /// schedule and never closes a cycle: the check locks the schedule row, so two concurrent inserts cannot each miss the
    /// other's edge. Retained rows are never deleted and never move to another project or schedule.
    /// </summary>
    public partial class TASK046_GuardScheduleHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION schedule.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'schedule.% % is retained and never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON schedule.project_schedule
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON schedule.schedule_activity
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON schedule.schedule_health_status
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_deletion();

                CREATE FUNCTION schedule.guard_project_owned() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.project_id, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'schedule.% %: it is one project''s and stays so', TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project_owned BEFORE UPDATE ON schedule.project_schedule
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_project_owned();
                CREATE TRIGGER guard_project_owned BEFORE UPDATE ON schedule.schedule_health_status
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_project_owned();

                CREATE FUNCTION schedule.is_frozen(p_project_schedule_id uuid) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (SELECT 1 FROM schedule.project_schedule s
                                   JOIN schedule.project_baseline b ON b.project_id = s.project_id
                                   WHERE s.id = p_project_schedule_id AND b.status = 'SUBMITTED');
                $$;

                CREATE FUNCTION schedule.guard_schedule_activity() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF schedule.is_frozen(NEW.project_schedule_id) THEN
                            RAISE EXCEPTION 'schedule_activity %: the plan is frozen while a baseline candidate is with WF-11', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_schedule_id, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.project_schedule_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'schedule_activity %: its schedule never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'CANCELLED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'schedule_activity % is CANCELLED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status AND (OLD.status, NEW.status) NOT IN (('PLANNED', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'schedule_activity %: % to % is not a step WF-03 takes', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF schedule.is_frozen(OLD.project_schedule_id)
                       AND (NEW.parent_activity_id, NEW.activity_kind, NEW.requested_start_date, NEW.planned_start_date, NEW.planned_finish_date,
                            NEW.planned_duration_days, NEW.status)
                           IS DISTINCT FROM
                           (OLD.parent_activity_id, OLD.activity_kind, OLD.requested_start_date, OLD.planned_start_date, OLD.planned_finish_date,
                            OLD.planned_duration_days, OLD.status) THEN
                        RAISE EXCEPTION 'schedule_activity %: the plan is frozen while a baseline candidate is with WF-11', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_schedule_activity
                    BEFORE INSERT OR UPDATE ON schedule.schedule_activity
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_schedule_activity();

                CREATE FUNCTION schedule.guard_schedule_dependency() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    v_predecessor_schedule uuid;
                    v_successor_schedule uuid;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'schedule_dependency %: a dependency is never changed, only removed and added again', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        SELECT a.project_schedule_id INTO v_successor_schedule FROM schedule.schedule_activity a WHERE a.id = OLD.successor_activity_id;
                        IF schedule.is_frozen(v_successor_schedule) THEN
                            RAISE EXCEPTION 'schedule_dependency %: the plan is frozen while a baseline candidate is with WF-11', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    SELECT a.project_schedule_id INTO v_predecessor_schedule FROM schedule.schedule_activity a WHERE a.id = NEW.predecessor_activity_id;
                    SELECT a.project_schedule_id INTO v_successor_schedule FROM schedule.schedule_activity a WHERE a.id = NEW.successor_activity_id;
                    IF v_predecessor_schedule IS DISTINCT FROM v_successor_schedule THEN
                        RAISE EXCEPTION 'schedule_dependency %: both ends are activities of one schedule', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_schedule_dependency_schedule';
                    END IF;

                    -- One dependency write per schedule at a time: each check then sees every edge committed before it.
                    PERFORM 1 FROM schedule.project_schedule s WHERE s.id = v_predecessor_schedule FOR UPDATE;

                    IF schedule.is_frozen(v_predecessor_schedule) THEN
                        RAISE EXCEPTION 'schedule_dependency %: the plan is frozen while a baseline candidate is with WF-11', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF EXISTS (WITH RECURSIVE reached (id) AS (
                                   SELECT NEW.successor_activity_id
                                   UNION
                                   SELECT d.successor_activity_id FROM schedule.schedule_dependency d JOIN reached r ON d.predecessor_activity_id = r.id)
                               SELECT 1 FROM reached WHERE id = NEW.predecessor_activity_id) THEN
                        RAISE EXCEPTION 'schedule_dependency %: % to % closes a cycle', NEW.id, NEW.predecessor_activity_id, NEW.successor_activity_id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_schedule_dependency_acyclic';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_schedule_dependency
                    BEFORE INSERT OR UPDATE OR DELETE ON schedule.schedule_dependency
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_schedule_dependency();

                CREATE FUNCTION schedule.guard_project_baseline() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'project_baseline % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NOT ((NEW.baseline_type = 'APPROVED' AND NEW.status = 'DRAFT') OR (NEW.baseline_type = 'DECLARED' AND NEW.status = 'ACTIVE')) THEN
                            RAISE EXCEPTION 'project_baseline %: a baseline is born DRAFT, or ACTIVE as a Declared Baseline', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_baseline_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.baseline_type, NEW.version_no, NEW.project_intake_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.baseline_type, OLD.version_no, OLD.project_intake_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'project_baseline %: its project, type, version and intake never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('SUPERSEDED', 'REJECTED', 'WITHDRAWN') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'project_baseline % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'ACTIVE'
                       AND to_jsonb(NEW) - ARRAY['status', 'superseded_at', 'superseded_by_baseline_id', 'updated_at', 'updated_by']
                           IS DISTINCT FROM to_jsonb(OLD) - ARRAY['status', 'superseded_at', 'superseded_by_baseline_id', 'updated_at', 'updated_by'] THEN
                        RAISE EXCEPTION 'project_baseline % is ACTIVE: it never changes, it is only superseded', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status THEN
                        IF (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('DRAFT', 'ACTIVE'), ('RETURNED', 'ACTIVE'), ('SUBMITTED', 'ACTIVE'),
                            ('SUBMITTED', 'RETURNED'), ('SUBMITTED', 'REJECTED'), ('SUBMITTED', 'WITHDRAWN'), ('ACTIVE', 'SUPERSEDED')) THEN
                            RAISE EXCEPTION 'project_baseline %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status = 'ACTIVE' AND NOT EXISTS (SELECT 1 FROM schedule.baseline_activity a WHERE a.project_baseline_id = NEW.id) THEN
                            RAISE EXCEPTION 'project_baseline %: an APPROVED baseline activates with its copy of the schedule', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_baseline_copy';
                        END IF;

                        IF NEW.status = 'SUPERSEDED'
                           AND NOT EXISTS (SELECT 1 FROM schedule.project_baseline b WHERE b.id = NEW.superseded_by_baseline_id AND b.project_id = NEW.project_id) THEN
                            RAISE EXCEPTION 'project_baseline %: it is superseded by another baseline of its project', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_baseline_superseded_by';
                        END IF;
                    END IF;

                    IF (OLD.status = 'RETURNED' AND NEW.status <> 'RETURNED') <> (NEW.revision_no IS DISTINCT FROM OLD.revision_no)
                       OR NEW.revision_no NOT IN (OLD.revision_no, OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'project_baseline %: the revision moves by one, and only when a RETURNED candidate is resubmitted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project_baseline
                    BEFORE INSERT OR UPDATE OR DELETE ON schedule.project_baseline
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_project_baseline();

                CREATE FUNCTION schedule.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'schedule.% holds baseline history and is never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON schedule.project_baseline
                    FOR EACH STATEMENT EXECUTE FUNCTION schedule.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON schedule.baseline_activity
                    FOR EACH STATEMENT EXECUTE FUNCTION schedule.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON schedule.baseline_dependency
                    FOR EACH STATEMENT EXECUTE FUNCTION schedule.refuse_truncation();

                CREATE FUNCTION schedule.refuse_copy_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'schedule.% % is a baseline''s copy and never changes: % refused', TG_TABLE_NAME, OLD.id, TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_copy_change BEFORE UPDATE OR DELETE ON schedule.baseline_activity
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_copy_change();
                CREATE TRIGGER refuse_copy_change BEFORE UPDATE OR DELETE ON schedule.baseline_dependency
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_copy_change();

                CREATE FUNCTION schedule.is_open_approved_baseline(p_project_baseline_id uuid) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (SELECT 1 FROM schedule.project_baseline b
                                   WHERE b.id = p_project_baseline_id AND b.baseline_type = 'APPROVED' AND b.status IN ('DRAFT', 'SUBMITTED', 'RETURNED'));
                $$;

                CREATE FUNCTION schedule.guard_baseline_activity() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT schedule.is_open_approved_baseline(NEW.project_baseline_id) THEN
                        RAISE EXCEPTION 'baseline_activity %: a baseline''s copy is written before it activates', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_baseline_activity_open_baseline';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM schedule.project_baseline b
                                   JOIN schedule.project_schedule s ON s.project_id = b.project_id
                                   JOIN schedule.schedule_activity a ON a.project_schedule_id = s.id
                                   WHERE b.id = NEW.project_baseline_id AND a.id = NEW.schedule_activity_id) THEN
                        RAISE EXCEPTION 'baseline_activity %: it copies an activity of its own project''s schedule', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_baseline_activity_project';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_baseline_activity BEFORE INSERT ON schedule.baseline_activity
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_baseline_activity();

                CREATE FUNCTION schedule.guard_baseline_dependency() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT schedule.is_open_approved_baseline(NEW.project_baseline_id) THEN
                        RAISE EXCEPTION 'baseline_dependency %: a baseline''s copy is written before it activates', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_baseline_dependency_open_baseline';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_baseline_dependency BEFORE INSERT ON schedule.baseline_dependency
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_baseline_dependency();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_baseline_dependency ON schedule.baseline_dependency;
                DROP FUNCTION schedule.guard_baseline_dependency();
                DROP TRIGGER guard_baseline_activity ON schedule.baseline_activity;
                DROP FUNCTION schedule.guard_baseline_activity();
                DROP FUNCTION schedule.is_open_approved_baseline(uuid);
                DROP TRIGGER refuse_copy_change ON schedule.baseline_dependency;
                DROP TRIGGER refuse_copy_change ON schedule.baseline_activity;
                DROP FUNCTION schedule.refuse_copy_change();
                DROP TRIGGER refuse_truncation ON schedule.baseline_dependency;
                DROP TRIGGER refuse_truncation ON schedule.baseline_activity;
                DROP TRIGGER refuse_truncation ON schedule.project_baseline;
                DROP FUNCTION schedule.refuse_truncation();
                DROP TRIGGER guard_project_baseline ON schedule.project_baseline;
                DROP FUNCTION schedule.guard_project_baseline();
                DROP TRIGGER guard_schedule_dependency ON schedule.schedule_dependency;
                DROP FUNCTION schedule.guard_schedule_dependency();
                DROP TRIGGER guard_schedule_activity ON schedule.schedule_activity;
                DROP FUNCTION schedule.guard_schedule_activity();
                DROP FUNCTION schedule.is_frozen(uuid);
                DROP TRIGGER guard_project_owned ON schedule.schedule_health_status;
                DROP TRIGGER guard_project_owned ON schedule.project_schedule;
                DROP FUNCTION schedule.guard_project_owned();
                DROP TRIGGER refuse_deletion ON schedule.schedule_health_status;
                DROP TRIGGER refuse_deletion ON schedule.schedule_activity;
                DROP TRIGGER refuse_deletion ON schedule.project_schedule;
                DROP FUNCTION schedule.refuse_deletion();
                """);
        }
    }
}
