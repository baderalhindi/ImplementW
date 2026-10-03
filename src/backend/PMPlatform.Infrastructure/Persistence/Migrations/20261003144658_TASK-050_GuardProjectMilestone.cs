using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-050, 5 of 6: WF-03's side of the shared milestone held in the database, whoever writes. A milestone is born PLANNED in
    /// its own project's schedule, never moves to another, and moves only PLANNED → ACHIEVED or PLANNED → CANCELLED; ACHIEVED and
    /// CANCELLED are final, and it is never deleted. The activity it completes is one of its schedule's. While a baseline candidate
    /// is SUBMITTED to WF-11 no milestone is added, re-dated or cancelled, because the candidate copies their dates as it
    /// activates. A baseline's milestone dates are written before it activates, only for its own project's milestones, and never
    /// changed. Reads only the <c>schedule</c> schema (README R-7).
    /// </summary>
    public partial class TASK050_GuardProjectMilestone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON schedule.project_milestone
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_deletion();

                CREATE FUNCTION schedule.guard_project_milestone() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'PLANNED' THEN
                            RAISE EXCEPTION 'project_milestone %: a milestone is born PLANNED', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_milestone_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM schedule.project_schedule s WHERE s.id = NEW.project_schedule_id AND s.project_id = NEW.project_id) THEN
                            RAISE EXCEPTION 'project_milestone %: it is a milestone of its own project''s schedule', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_milestone_project';
                        END IF;

                        IF schedule.is_frozen(NEW.project_schedule_id) THEN
                            RAISE EXCEPTION 'project_milestone %: the plan is frozen while a baseline candidate is with WF-11', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    ELSE
                        IF (NEW.id, NEW.project_id, NEW.project_schedule_id, NEW.created_at, NEW.created_by)
                           IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.project_schedule_id, OLD.created_at, OLD.created_by) THEN
                            RAISE EXCEPTION 'project_milestone %: its project and schedule never change', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.status IN ('ACHIEVED', 'CANCELLED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                            RAISE EXCEPTION 'project_milestone % is % and changes no more', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status IS DISTINCT FROM OLD.status AND (OLD.status, NEW.status) NOT IN (('PLANNED', 'ACHIEVED'), ('PLANNED', 'CANCELLED')) THEN
                            RAISE EXCEPTION 'project_milestone %: % to % is not a step WF-03 takes', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        -- WF-05's acceptance (→ ACHIEVED) is not a change to the plan, so it is not frozen.
                        IF schedule.is_frozen(OLD.project_schedule_id)
                           AND (NEW.forecast_date IS DISTINCT FROM OLD.forecast_date OR (NEW.status = 'CANCELLED' AND OLD.status <> 'CANCELLED')) THEN
                            RAISE EXCEPTION 'project_milestone %: the plan is frozen while a baseline candidate is with WF-11', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    END IF;

                    IF NEW.schedule_activity_id IS NOT NULL
                       AND (TG_OP = 'INSERT' OR NEW.schedule_activity_id IS DISTINCT FROM OLD.schedule_activity_id)
                       AND NOT EXISTS (SELECT 1 FROM schedule.schedule_activity a
                                       WHERE a.id = NEW.schedule_activity_id AND a.project_schedule_id = NEW.project_schedule_id) THEN
                        RAISE EXCEPTION 'project_milestone %: the activity it completes is one of its own schedule''s', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_project_milestone_activity';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project_milestone
                    BEFORE INSERT OR UPDATE ON schedule.project_milestone
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_project_milestone();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON schedule.baseline_milestone
                    FOR EACH STATEMENT EXECUTE FUNCTION schedule.refuse_truncation();
                CREATE TRIGGER refuse_copy_change BEFORE UPDATE OR DELETE ON schedule.baseline_milestone
                    FOR EACH ROW EXECUTE FUNCTION schedule.refuse_copy_change();

                CREATE FUNCTION schedule.guard_baseline_milestone() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT schedule.is_open_approved_baseline(NEW.project_baseline_id) THEN
                        RAISE EXCEPTION 'baseline_milestone %: a baseline''s copy is written before it activates', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_baseline_milestone_open_baseline';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM schedule.project_baseline b
                                   JOIN schedule.project_milestone m ON m.project_id = b.project_id
                                   WHERE b.id = NEW.project_baseline_id AND m.id = NEW.project_milestone_id) THEN
                        RAISE EXCEPTION 'baseline_milestone %: it copies a milestone of its own project', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_baseline_milestone_project';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_baseline_milestone BEFORE INSERT ON schedule.baseline_milestone
                    FOR EACH ROW EXECUTE FUNCTION schedule.guard_baseline_milestone();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_baseline_milestone ON schedule.baseline_milestone;
                DROP FUNCTION schedule.guard_baseline_milestone();
                DROP TRIGGER refuse_copy_change ON schedule.baseline_milestone;
                DROP TRIGGER refuse_truncation ON schedule.baseline_milestone;
                DROP TRIGGER guard_project_milestone ON schedule.project_milestone;
                DROP FUNCTION schedule.guard_project_milestone();
                DROP TRIGGER refuse_deletion ON schedule.project_milestone;
                """);
        }
    }
}
