using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-044, 3 of 3: progress history is fixed in the database, whoever writes to it. A published snapshot is never
    /// updated, deleted or truncated (APPEND_ONLY), so publishing a period can never alter an earlier one; it records its own
    /// project's submission and period. A reporting period is never deleted (RETAIN), its dates never change, and it moves
    /// only from OPEN to CLOSED, after which it changes no more. A submission is born DRAFT, or SUBMITTED as the ADR-014
    /// opening position, into an OPEN period of its own project; it moves only along the four edges of
    /// <c>ProgressWorkflow</c>; its figures, override and narrative change only while DRAFT; RETURNED and PUBLISHED are final;
    /// only a DRAFT is deleted (HARD_DRAFT). A project's live health row is never deleted (RETAIN) and never moves to
    /// another project.
    /// </summary>
    public partial class TASK044_GuardProgressHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION progress.refuse_snapshot_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'published_progress_snapshot is PUBLISHED/OFFICIAL and never changes: % refused', TG_OP
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_snapshot_change
                    BEFORE UPDATE OR DELETE ON progress.published_progress_snapshot
                    FOR EACH ROW EXECUTE FUNCTION progress.refuse_snapshot_change();
                CREATE TRIGGER refuse_snapshot_truncate
                    BEFORE TRUNCATE ON progress.published_progress_snapshot
                    FOR EACH STATEMENT EXECUTE FUNCTION progress.refuse_snapshot_change();

                CREATE FUNCTION progress.guard_published_progress_snapshot() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM progress.progress_submission s
                                   WHERE s.id = NEW.progress_submission_id AND s.project_id = NEW.project_id AND s.reporting_cycle_id = NEW.reporting_cycle_id) THEN
                        RAISE EXCEPTION 'published_progress_snapshot %: it records a submission of its own project and period', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_published_progress_snapshot_subject';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_published_progress_snapshot
                    BEFORE INSERT ON progress.published_progress_snapshot
                    FOR EACH ROW EXECUTE FUNCTION progress.guard_published_progress_snapshot();

                CREATE FUNCTION progress.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'progress.% % is progress history and is never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON progress.reporting_cycle
                    FOR EACH ROW EXECUTE FUNCTION progress.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON progress.project_health_status
                    FOR EACH ROW EXECUTE FUNCTION progress.refuse_deletion();

                CREATE FUNCTION progress.guard_reporting_cycle() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.project_id, NEW.period_start, NEW.period_end, NEW.due_date, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.period_start, OLD.period_end, OLD.due_date, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'reporting_cycle %: its project and dates never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'CLOSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'reporting_cycle % is CLOSED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_reporting_cycle
                    BEFORE UPDATE ON progress.reporting_cycle
                    FOR EACH ROW EXECUTE FUNCTION progress.guard_reporting_cycle();

                CREATE FUNCTION progress.guard_progress_submission() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'progress_submission % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NOT (NEW.status = 'DRAFT' OR (NEW.status = 'SUBMITTED' AND NEW.project_intake_id IS NOT NULL)) THEN
                            RAISE EXCEPTION 'progress_submission %: a revision is born DRAFT, or SUBMITTED as the opening position', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_progress_submission_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM progress.reporting_cycle c
                                       WHERE c.id = NEW.reporting_cycle_id AND c.project_id = NEW.project_id AND c.status = 'OPEN') THEN
                            RAISE EXCEPTION 'progress_submission %: a revision is written into an OPEN period of its own project', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_progress_submission_open_period';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.reporting_cycle_id, NEW.revision_no, NEW.project_intake_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.reporting_cycle_id, OLD.revision_no, OLD.project_intake_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'progress_submission %: its project, period, revision and intake never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('RETURNED', 'PUBLISHED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'progress_submission % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'DRAFT'
                       AND (NEW.actual_percent_calculated, NEW.actual_percent_override, NEW.override_reason, NEW.override_reason_lang,
                            NEW.planned_percent, NEW.baseline_id, NEW.narrative, NEW.narrative_lang)
                           IS DISTINCT FROM
                           (OLD.actual_percent_calculated, OLD.actual_percent_override, OLD.override_reason, OLD.override_reason_lang,
                            OLD.planned_percent, OLD.baseline_id, OLD.narrative, OLD.narrative_lang) THEN
                        RAISE EXCEPTION 'progress_submission % is %: its figures and narrative were fixed on submission', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status
                       AND (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'PUBLISHED')) THEN
                        RAISE EXCEPTION 'progress_submission %: % to % is not a step of its review', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_progress_submission
                    BEFORE INSERT OR UPDATE OR DELETE ON progress.progress_submission
                    FOR EACH ROW EXECUTE FUNCTION progress.guard_progress_submission();

                CREATE FUNCTION progress.guard_project_health_status() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.project_id, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'project_health_status %: it is one project''s and stays so', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_project_health_status
                    BEFORE UPDATE ON progress.project_health_status
                    FOR EACH ROW EXECUTE FUNCTION progress.guard_project_health_status();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_project_health_status ON progress.project_health_status;
                DROP FUNCTION progress.guard_project_health_status();
                DROP TRIGGER guard_progress_submission ON progress.progress_submission;
                DROP FUNCTION progress.guard_progress_submission();
                DROP TRIGGER guard_reporting_cycle ON progress.reporting_cycle;
                DROP FUNCTION progress.guard_reporting_cycle();
                DROP TRIGGER refuse_deletion ON progress.project_health_status;
                DROP TRIGGER refuse_deletion ON progress.reporting_cycle;
                DROP FUNCTION progress.refuse_deletion();
                DROP TRIGGER guard_published_progress_snapshot ON progress.published_progress_snapshot;
                DROP FUNCTION progress.guard_published_progress_snapshot();
                DROP TRIGGER refuse_snapshot_truncate ON progress.published_progress_snapshot;
                DROP TRIGGER refuse_snapshot_change ON progress.published_progress_snapshot;
                DROP FUNCTION progress.refuse_snapshot_change();
                """);
        }
    }
}
