using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-048, 3 of 3: WF-04's rules held in the database, whoever writes. A task is born NOT_STARTED and moves only along the
    /// edges of <c>ProjectTaskWorkflow</c> — there is no BLOCKED → COMPLETED — and CANCELLED is final; a COMPLETED task is
    /// reopened with its count raised by one; its actual start never moves once set. A start waits for its FS and SS
    /// predecessors, a completion for its FF and SF predecessors and its live subtasks; a task is cancelled only with no live
    /// subtask and no dependency. A subtask is one level deep, under a live parent of its project. A dependency joins two live leaf
    /// tasks of one project, is never updated and never closes a cycle. Each check that reads other rows first takes the
    /// project's advisory lock, the same <c>project_task.lock_project</c> the application takes, so concurrent writers cannot
    /// each miss the other's change. Tasks and Activity Execution Progress are retained — never deleted or truncated — and never
    /// move to another project.
    /// </summary>
    public partial class TASK048_GuardProjectTask : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION project_task.lock_project(p_project_id uuid) RETURNS void
                LANGUAGE sql AS $$
                    SELECT pg_advisory_xact_lock(hashtextextended('project_task:' || p_project_id::text, 0));
                $$;

                CREATE FUNCTION project_task.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'project_task.% % is retained and never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON project_task.project_task
                    FOR EACH ROW EXECUTE FUNCTION project_task.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON project_task.activity_execution_progress
                    FOR EACH ROW EXECUTE FUNCTION project_task.refuse_deletion();

                CREATE FUNCTION project_task.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'project_task.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON project_task.project_task
                    FOR EACH STATEMENT EXECUTE FUNCTION project_task.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON project_task.activity_execution_progress
                    FOR EACH STATEMENT EXECUTE FUNCTION project_task.refuse_truncation();

                CREATE FUNCTION project_task.has_live_subtasks(p_task_id uuid) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (SELECT 1 FROM project_task.project_task s WHERE s.parent_task_id = p_task_id AND s.status <> 'CANCELLED');
                $$;

                CREATE FUNCTION project_task.guard_project_task() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    v_parent project_task.project_task%ROWTYPE;
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'NOT_STARTED' OR NEW.reopened_count <> 0 OR NEW.actual_percent_complete IS NOT NULL THEN
                            RAISE EXCEPTION 'project_task %: a task is born NOT_STARTED', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.parent_task_id IS NOT NULL THEN
                            PERFORM project_task.lock_project(NEW.project_id);
                            SELECT * INTO v_parent FROM project_task.project_task p WHERE p.id = NEW.parent_task_id;
                            IF v_parent.project_id IS DISTINCT FROM NEW.project_id OR v_parent.parent_task_id IS NOT NULL
                               OR v_parent.status IN ('COMPLETED', 'CANCELLED')
                               OR EXISTS (SELECT 1 FROM project_task.task_dependency d
                                          WHERE d.predecessor_task_id = v_parent.id OR d.successor_task_id = v_parent.id) THEN
                                RAISE EXCEPTION 'project_task %: a subtask is one level under a live top-level task of its project that is no dependency end', NEW.id
                                    USING ERRCODE = 'restrict_violation';
                            END IF;
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.parent_task_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.parent_task_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'project_task %: its project and parent never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'CANCELLED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'project_task % is CANCELLED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.actual_start_date IS NOT NULL AND NEW.actual_start_date IS DISTINCT FROM OLD.actual_start_date THEN
                        RAISE EXCEPTION 'project_task %: its actual start never moves', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.reopened_count <> OLD.reopened_count + (CASE WHEN (OLD.status, NEW.status) = ('COMPLETED', 'IN_PROGRESS') THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION 'project_task %: the reopen count rises by one with each reopen, and only then', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.actual_percent_complete IS DISTINCT FROM OLD.actual_percent_complete
                       AND NOT (OLD.status = NEW.status AND NEW.status IN ('IN_PROGRESS', 'BLOCKED'))
                       AND NOT ((OLD.status, NEW.status) = ('IN_PROGRESS', 'COMPLETED') AND NEW.actual_percent_complete = 100) THEN
                        RAISE EXCEPTION 'project_task %: an actual percentage is entered while the task is IN_PROGRESS or BLOCKED', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS NOT DISTINCT FROM OLD.status THEN
                        RETURN NEW;
                    END IF;

                    IF (OLD.status, NEW.status) NOT IN (
                        ('NOT_STARTED', 'IN_PROGRESS'), ('NOT_STARTED', 'BLOCKED'), ('IN_PROGRESS', 'BLOCKED'), ('BLOCKED', 'NOT_STARTED'),
                        ('BLOCKED', 'IN_PROGRESS'), ('IN_PROGRESS', 'COMPLETED'), ('COMPLETED', 'IN_PROGRESS'),
                        ('NOT_STARTED', 'CANCELLED'), ('IN_PROGRESS', 'CANCELLED'), ('BLOCKED', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'project_task %: % to % is not a step of the task''s state machine', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    -- The checks below read the project's other tasks and dependencies: one writer per project at a time.
                    PERFORM project_task.lock_project(NEW.project_id);

                    IF (OLD.status, NEW.status) = ('NOT_STARTED', 'IN_PROGRESS')
                       AND EXISTS (SELECT 1 FROM project_task.task_dependency d JOIN project_task.project_task p ON p.id = d.predecessor_task_id
                                   WHERE d.successor_task_id = NEW.id
                                     AND ((d.dependency_type = 'FS' AND p.status <> 'COMPLETED') OR (d.dependency_type = 'SS' AND p.actual_start_date IS NULL))) THEN
                        RAISE EXCEPTION 'project_task %: an FS or SS predecessor keeps it from starting', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'COMPLETED'
                       AND (EXISTS (SELECT 1 FROM project_task.task_dependency d JOIN project_task.project_task p ON p.id = d.predecessor_task_id
                                    WHERE d.successor_task_id = NEW.id
                                      AND ((d.dependency_type = 'FF' AND p.status <> 'COMPLETED') OR (d.dependency_type = 'SF' AND p.actual_start_date IS NULL)))
                            OR EXISTS (SELECT 1 FROM project_task.project_task s WHERE s.parent_task_id = NEW.id AND s.status NOT IN ('COMPLETED', 'CANCELLED'))) THEN
                        RAISE EXCEPTION 'project_task %: an FF or SF predecessor, or a live subtask, keeps it from completing', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'CANCELLED'
                       AND (project_task.has_live_subtasks(NEW.id)
                            OR EXISTS (SELECT 1 FROM project_task.task_dependency d WHERE d.predecessor_task_id = NEW.id OR d.successor_task_id = NEW.id)) THEN
                        RAISE EXCEPTION 'project_task %: a task with a live subtask or a dependency is not cancelled', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project_task
                    BEFORE INSERT OR UPDATE ON project_task.project_task
                    FOR EACH ROW EXECUTE FUNCTION project_task.guard_project_task();

                CREATE FUNCTION project_task.guard_task_dependency() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    v_predecessor project_task.project_task%ROWTYPE;
                    v_successor project_task.project_task%ROWTYPE;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'task_dependency %: a dependency is never changed, only removed and added again', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    SELECT * INTO v_predecessor FROM project_task.project_task t WHERE t.id = NEW.predecessor_task_id;
                    SELECT * INTO v_successor FROM project_task.project_task t WHERE t.id = NEW.successor_task_id;
                    IF v_predecessor.project_id IS DISTINCT FROM v_successor.project_id THEN
                        RAISE EXCEPTION 'task_dependency %: both ends are tasks of one project', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_task_dependency_project';
                    END IF;

                    -- One dependency write per project at a time: each check then sees every edge committed before it.
                    PERFORM project_task.lock_project(v_predecessor.project_id);

                    IF v_predecessor.status = 'CANCELLED' OR v_successor.status = 'CANCELLED'
                       OR project_task.has_live_subtasks(v_predecessor.id) OR project_task.has_live_subtasks(v_successor.id) THEN
                        RAISE EXCEPTION 'task_dependency %: both ends are live leaf tasks', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF EXISTS (WITH RECURSIVE reach (id) AS (
                                   SELECT NEW.successor_task_id
                                   UNION
                                   SELECT d.successor_task_id FROM project_task.task_dependency d JOIN reach r ON d.predecessor_task_id = r.id)
                               SELECT 1 FROM reach WHERE reach.id = NEW.predecessor_task_id) THEN
                        RAISE EXCEPTION 'task_dependency %: it would close a cycle', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_task_dependency_acyclic';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_task_dependency
                    BEFORE INSERT OR UPDATE ON project_task.task_dependency
                    FOR EACH ROW EXECUTE FUNCTION project_task.guard_task_dependency();

                CREATE FUNCTION project_task.guard_activity_execution_progress() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.project_id, NEW.schedule_activity_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.schedule_activity_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'activity_execution_progress %: it is one activity''s and stays so', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_activity_execution_progress
                    BEFORE UPDATE ON project_task.activity_execution_progress
                    FOR EACH ROW EXECUTE FUNCTION project_task.guard_activity_execution_progress();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_activity_execution_progress ON project_task.activity_execution_progress;
                DROP FUNCTION project_task.guard_activity_execution_progress();
                DROP TRIGGER guard_task_dependency ON project_task.task_dependency;
                DROP FUNCTION project_task.guard_task_dependency();
                DROP TRIGGER guard_project_task ON project_task.project_task;
                DROP FUNCTION project_task.guard_project_task();
                DROP FUNCTION project_task.has_live_subtasks(uuid);
                DROP TRIGGER refuse_truncation ON project_task.activity_execution_progress;
                DROP TRIGGER refuse_truncation ON project_task.project_task;
                DROP FUNCTION project_task.refuse_truncation();
                DROP TRIGGER refuse_deletion ON project_task.activity_execution_progress;
                DROP TRIGGER refuse_deletion ON project_task.project_task;
                DROP FUNCTION project_task.refuse_deletion();
                DROP FUNCTION project_task.lock_project(uuid);
                """);
        }
    }
}
