using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-052, 3 of 3: WF-14's history held in the database, whoever writes. An INTEGRATED field never returns to manual entry
    /// (ADR-008). A commitment or target version is born DRAFT after every earlier version, moves only along the edges of
    /// <c>ApprovedVersionWorkflow</c>, keeps the figures WF-11 reviewed, and once ACTIVE changes only by being superseded by a
    /// later version. A period's financial update moves only along <c>FinancialUpdateWorkflow</c> and its figures change only while
    /// DRAFT. A published snapshot is never changed or deleted. A measurement pins the ACTIVE target version of its assignment when
    /// it is recorded and never re-pins it; its value and rating change only while DRAFT. Only DRAFTs are deleted, assignments and
    /// source modes never, and no table is truncated. Reads only the <c>financial_kpi</c> schema (README R-7).
    /// </summary>
    public partial class TASK052_GuardFinancialKpiHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION financial_kpi.guard_financial_source_mode() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'financial_source_mode % is never deleted (RETAIN)', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.field_code, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.field_code, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'financial_source_mode %: its project and field never change', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.source_mode = 'INTEGRATED' AND NEW.source_mode <> 'INTEGRATED' THEN
                        RAISE EXCEPTION 'financial_source_mode %: an INTEGRATED field never returns to manual entry (ADR-008)', OLD.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_source_mode_integrated';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_financial_source_mode BEFORE UPDATE OR DELETE ON financial_kpi.financial_source_mode
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_financial_source_mode();

                CREATE FUNCTION financial_kpi.guard_financial_commitment() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'financial_commitment % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' AND NOT (NEW.commitment_type = 'DECLARED_BUDGET' AND NEW.status = 'ACTIVE') THEN
                            RAISE EXCEPTION 'financial_commitment %: a version is born DRAFT; only a declared budget is born ACTIVE (ADR-014)', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_commitment_born';
                        END IF;

                        IF EXISTS (SELECT 1 FROM financial_kpi.financial_commitment c
                                   WHERE c.project_id = NEW.project_id AND c.commitment_type = NEW.commitment_type AND c.version_no >= NEW.version_no) THEN
                            RAISE EXCEPTION 'financial_commitment %: a new version comes after every version of its project and type', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_commitment_version_order';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.commitment_type, NEW.version_no, NEW.project_intake_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.commitment_type, OLD.version_no, OLD.project_intake_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'financial_commitment %: its project, type and version never change', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('SUPERSEDED', 'REJECTED', 'WITHDRAWN') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'financial_commitment % is % and changes no more', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'ACTIVE'
                       AND to_jsonb(NEW) - ARRAY['status', 'superseded_by_commitment_id', 'updated_at', 'updated_by']
                           IS DISTINCT FROM to_jsonb(OLD) - ARRAY['status', 'superseded_by_commitment_id', 'updated_at', 'updated_by'] THEN
                        RAISE EXCEPTION 'financial_commitment % is ACTIVE: the budget of record is never edited, a change is a new version', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status NOT IN ('DRAFT', 'RETURNED')
                       AND (NEW.amount_sar, NEW.source_type, NEW.source_reference, NEW.as_of_date, NEW.entered_by_user_id, NEW.change_authorization_id)
                           IS DISTINCT FROM (OLD.amount_sar, OLD.source_type, OLD.source_reference, OLD.as_of_date, OLD.entered_by_user_id, OLD.change_authorization_id) THEN
                        RAISE EXCEPTION 'financial_commitment % is %: the figures WF-11 reviews never change', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.effective_from IS DISTINCT FROM OLD.effective_from
                       AND NOT (OLD.status IN ('DRAFT', 'RETURNED') OR (OLD.status = 'SUBMITTED' AND NEW.status = 'ACTIVE' AND OLD.effective_from IS NULL)) THEN
                        RAISE EXCEPTION 'financial_commitment %: its effective date is set while it is edited, or on activation when none was given', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no IS DISTINCT FROM OLD.revision_no
                       AND NOT (OLD.status = 'RETURNED' AND NEW.status = 'SUBMITTED' AND NEW.revision_no = OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'financial_commitment %: a revision is the next one, on resubmission after a return', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status THEN
                        IF (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('SUBMITTED', 'ACTIVE'), ('SUBMITTED', 'RETURNED'),
                            ('SUBMITTED', 'REJECTED'), ('SUBMITTED', 'WITHDRAWN'), ('ACTIVE', 'SUPERSEDED')) THEN
                            RAISE EXCEPTION 'financial_commitment %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status = 'SUPERSEDED'
                           AND NOT EXISTS (SELECT 1 FROM financial_kpi.financial_commitment c
                                           WHERE c.id = NEW.superseded_by_commitment_id AND c.project_id = NEW.project_id
                                             AND c.commitment_type = NEW.commitment_type AND c.version_no > NEW.version_no) THEN
                            RAISE EXCEPTION 'financial_commitment %: it is superseded by a later version of its project and type', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_commitment_superseded_by';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_financial_commitment BEFORE INSERT OR UPDATE OR DELETE ON financial_kpi.financial_commitment
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_financial_commitment();

                CREATE FUNCTION financial_kpi.guard_financial_progress_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'financial_progress_update % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' AND NEW.project_intake_id IS NULL THEN
                            RAISE EXCEPTION 'financial_progress_update %: a revision is born DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_progress_update_born';
                        END IF;

                        IF EXISTS (SELECT 1 FROM financial_kpi.financial_progress_update u
                                   WHERE u.reporting_cycle_id = NEW.reporting_cycle_id AND (u.revision_no >= NEW.revision_no OR u.status = 'PUBLISHED')) THEN
                            RAISE EXCEPTION 'financial_progress_update %: a new revision comes after every revision of an unpublished period', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_financial_progress_update_revision_order';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.reporting_cycle_id, NEW.revision_no, NEW.project_intake_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.reporting_cycle_id, OLD.revision_no, OLD.project_intake_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'financial_progress_update %: its project, period and revision never change', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('RETURNED', 'PUBLISHED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'financial_progress_update % is % and changes no more', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'DRAFT'
                       AND (NEW.actual_expenditure_to_date_sar, NEW.forecast_at_completion_sar, NEW.value_status, NEW.narrative, NEW.narrative_lang,
                            NEW.source_type, NEW.source_reference, NEW.as_of_date, NEW.entered_by_user_id, NEW.submitted_by_user_id, NEW.submitted_at)
                           IS DISTINCT FROM
                           (OLD.actual_expenditure_to_date_sar, OLD.forecast_at_completion_sar, OLD.value_status, OLD.narrative, OLD.narrative_lang,
                            OLD.source_type, OLD.source_reference, OLD.as_of_date, OLD.entered_by_user_id, OLD.submitted_by_user_id, OLD.submitted_at) THEN
                        RAISE EXCEPTION 'financial_progress_update % is %: the figures under review never change', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status
                       AND (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'PUBLISHED')) THEN
                        RAISE EXCEPTION 'financial_progress_update %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_financial_progress_update BEFORE INSERT OR UPDATE OR DELETE ON financial_kpi.financial_progress_update
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_financial_progress_update();

                CREATE FUNCTION financial_kpi.guard_published_financial_snapshot() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NOT EXISTS (SELECT 1 FROM financial_kpi.financial_progress_update u
                                       WHERE u.id = NEW.financial_progress_update_id AND u.project_id = NEW.project_id AND u.reporting_cycle_id = NEW.reporting_cycle_id) THEN
                            RAISE EXCEPTION 'published_financial_snapshot %: it publishes an update of its own project and period', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_published_financial_snapshot_update';
                        END IF;

                        IF NEW.financial_commitment_id IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM financial_kpi.financial_commitment c
                                           WHERE c.id = NEW.financial_commitment_id AND c.project_id = NEW.project_id
                                             AND c.commitment_type = 'APPROVED_BUDGET' AND c.amount_sar = NEW.approved_budget_sar) THEN
                            RAISE EXCEPTION 'published_financial_snapshot %: its budget is a copy of an Approved Budget version of its project', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_published_financial_snapshot_budget_copy';
                        END IF;

                        RETURN NEW;
                    END IF;

                    RAISE EXCEPTION 'published_financial_snapshot % is PUBLISHED/OFFICIAL and is never changed or deleted (APPEND_ONLY)', OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER guard_published_financial_snapshot BEFORE INSERT OR UPDATE OR DELETE ON financial_kpi.published_financial_snapshot
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_published_financial_snapshot();

                CREATE FUNCTION financial_kpi.guard_kpi_assignment() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'kpi_assignment % is never deleted (RETAIN)', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.kpi_definition_id, NEW.assigned_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.kpi_definition_id, OLD.assigned_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'kpi_assignment %: its project and KPI never change', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'RETIRED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'kpi_assignment % is RETIRED and changes no more', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_kpi_assignment BEFORE UPDATE OR DELETE ON financial_kpi.kpi_assignment
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_kpi_assignment();

                CREATE FUNCTION financial_kpi.guard_kpi_target_version() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'kpi_target_version % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'kpi_target_version %: a version is born DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_kpi_target_version_born';
                        END IF;

                        IF EXISTS (SELECT 1 FROM financial_kpi.kpi_target_version t
                                   WHERE t.kpi_assignment_id = NEW.kpi_assignment_id AND t.version_no >= NEW.version_no) THEN
                            RAISE EXCEPTION 'kpi_target_version %: a new version comes after every version of its assignment', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_kpi_target_version_version_order';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.kpi_assignment_id, NEW.version_no, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.kpi_assignment_id, OLD.version_no, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'kpi_target_version %: its assignment and version never change', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('SUPERSEDED', 'REJECTED', 'WITHDRAWN') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'kpi_target_version % is % and changes no more', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'ACTIVE'
                       AND to_jsonb(NEW) - ARRAY['status', 'superseded_by_target_version_id', 'updated_at', 'updated_by']
                           IS DISTINCT FROM to_jsonb(OLD) - ARRAY['status', 'superseded_by_target_version_id', 'updated_at', 'updated_by'] THEN
                        RAISE EXCEPTION 'kpi_target_version % is ACTIVE: an approved target is never edited, a new target is a new version', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status NOT IN ('DRAFT', 'RETURNED')
                       AND (NEW.target_value, NEW.green_threshold, NEW.amber_threshold) IS DISTINCT FROM (OLD.target_value, OLD.green_threshold, OLD.amber_threshold) THEN
                        RAISE EXCEPTION 'kpi_target_version % is %: the target WF-11 reviews never changes', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.effective_from IS DISTINCT FROM OLD.effective_from
                       AND NOT (OLD.status IN ('DRAFT', 'RETURNED') OR (OLD.status = 'SUBMITTED' AND NEW.status = 'ACTIVE' AND OLD.effective_from IS NULL)) THEN
                        RAISE EXCEPTION 'kpi_target_version %: its effective date is set while it is edited, or on activation when none was given', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no IS DISTINCT FROM OLD.revision_no
                       AND NOT (OLD.status = 'RETURNED' AND NEW.status = 'SUBMITTED' AND NEW.revision_no = OLD.revision_no + 1) THEN
                        RAISE EXCEPTION 'kpi_target_version %: a revision is the next one, on resubmission after a return', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status THEN
                        IF (OLD.status, NEW.status) NOT IN
                           (('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('SUBMITTED', 'ACTIVE'), ('SUBMITTED', 'RETURNED'),
                            ('SUBMITTED', 'REJECTED'), ('SUBMITTED', 'WITHDRAWN'), ('ACTIVE', 'SUPERSEDED')) THEN
                            RAISE EXCEPTION 'kpi_target_version %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status = 'SUPERSEDED'
                           AND NOT EXISTS (SELECT 1 FROM financial_kpi.kpi_target_version t
                                           WHERE t.id = NEW.superseded_by_target_version_id AND t.kpi_assignment_id = NEW.kpi_assignment_id
                                             AND t.version_no > NEW.version_no) THEN
                            RAISE EXCEPTION 'kpi_target_version %: it is superseded by a later version of its assignment', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_kpi_target_version_superseded_by';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_kpi_target_version BEFORE INSERT OR UPDATE OR DELETE ON financial_kpi.kpi_target_version
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_kpi_target_version();

                CREATE FUNCTION financial_kpi.guard_kpi_measurement() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'kpi_measurement % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'kpi_measurement %: a measurement is born DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_kpi_measurement_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM financial_kpi.kpi_target_version t
                                       WHERE t.id = NEW.kpi_target_version_id AND t.kpi_assignment_id = NEW.kpi_assignment_id AND t.status = 'ACTIVE') THEN
                            RAISE EXCEPTION 'kpi_measurement %: it pins the ACTIVE target version of its own assignment', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_kpi_measurement_pinned_target';
                        END IF;

                        RETURN NEW;
                    END IF;

                    -- The pin: a measurement keeps the target version it was recorded against, whatever happens to the target after.
                    IF (NEW.id, NEW.kpi_assignment_id, NEW.kpi_target_version_id, NEW.period_start, NEW.period_end, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.kpi_assignment_id, OLD.kpi_target_version_id, OLD.period_start, OLD.period_end, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'kpi_measurement %: its assignment, period and pinned target version never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'PUBLISHED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'kpi_measurement % is PUBLISHED and changes no more', OLD.id USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'DRAFT'
                       AND (NEW.measured_value, NEW.value_status, NEW.rag_status, NEW.as_of_date, NEW.recorded_by_user_id, NEW.narrative, NEW.narrative_lang, NEW.submitted_at)
                           IS DISTINCT FROM
                           (OLD.measured_value, OLD.value_status, OLD.rag_status, OLD.as_of_date, OLD.recorded_by_user_id, OLD.narrative, OLD.narrative_lang, OLD.submitted_at) THEN
                        RAISE EXCEPTION 'kpi_measurement % is %: its value and rating never change', OLD.id, OLD.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status AND (OLD.status, NEW.status) NOT IN (('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'PUBLISHED')) THEN
                        RAISE EXCEPTION 'kpi_measurement %: % to % is not a step of its workflow', OLD.id, OLD.status, NEW.status USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_kpi_measurement BEFORE INSERT OR UPDATE OR DELETE ON financial_kpi.kpi_measurement
                    FOR EACH ROW EXECUTE FUNCTION financial_kpi.guard_kpi_measurement();

                CREATE FUNCTION financial_kpi.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'financial_kpi.% holds financial and KPI history and is never truncated', TG_TABLE_NAME USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.financial_source_mode
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.financial_commitment
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.financial_commitment_line
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.financial_progress_update
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.financial_progress_update_line
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.published_financial_snapshot
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.kpi_assignment
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.kpi_target_version
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON financial_kpi.kpi_measurement
                    FOR EACH STATEMENT EXECUTE FUNCTION financial_kpi.refuse_truncation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER refuse_truncation ON financial_kpi.financial_source_mode;
                DROP TRIGGER refuse_truncation ON financial_kpi.financial_commitment;
                DROP TRIGGER refuse_truncation ON financial_kpi.financial_commitment_line;
                DROP TRIGGER refuse_truncation ON financial_kpi.financial_progress_update;
                DROP TRIGGER refuse_truncation ON financial_kpi.financial_progress_update_line;
                DROP TRIGGER refuse_truncation ON financial_kpi.published_financial_snapshot;
                DROP TRIGGER refuse_truncation ON financial_kpi.kpi_assignment;
                DROP TRIGGER refuse_truncation ON financial_kpi.kpi_target_version;
                DROP TRIGGER refuse_truncation ON financial_kpi.kpi_measurement;
                DROP FUNCTION financial_kpi.refuse_truncation();
                DROP TRIGGER guard_financial_source_mode ON financial_kpi.financial_source_mode;
                DROP FUNCTION financial_kpi.guard_financial_source_mode();
                DROP TRIGGER guard_financial_commitment ON financial_kpi.financial_commitment;
                DROP FUNCTION financial_kpi.guard_financial_commitment();
                DROP TRIGGER guard_financial_progress_update ON financial_kpi.financial_progress_update;
                DROP FUNCTION financial_kpi.guard_financial_progress_update();
                DROP TRIGGER guard_published_financial_snapshot ON financial_kpi.published_financial_snapshot;
                DROP FUNCTION financial_kpi.guard_published_financial_snapshot();
                DROP TRIGGER guard_kpi_assignment ON financial_kpi.kpi_assignment;
                DROP FUNCTION financial_kpi.guard_kpi_assignment();
                DROP TRIGGER guard_kpi_target_version ON financial_kpi.kpi_target_version;
                DROP FUNCTION financial_kpi.guard_kpi_target_version();
                DROP TRIGGER guard_kpi_measurement ON financial_kpi.kpi_measurement;
                DROP FUNCTION financial_kpi.guard_kpi_measurement();
                """);
        }
    }
}
