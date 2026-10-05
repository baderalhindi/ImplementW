using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-055, 3 of 3: WF-06's history held in the database, whoever writes. An assessment version and its impacts are never
    /// changed or deleted, a version comes after every earlier one of its risk, an impact is written with its assessment, and a
    /// CLOSED risk is not assessed: a recorded rating is never rewritten. A risk is born IDENTIFIED, moves only along the edges of
    /// <c>RiskWorkflow</c>, changes no more while CLOSED but by its reopen, raises its reopen count by one with each reopen and only
    /// then, and keeps its materialisation once set. An acceptance is born ACTIVE and only expires or is revoked; an action is born
    /// PLANNED and only moves forward. Nothing is deleted and no table is truncated. Reads only the <c>risk</c> schema (README R-7).
    /// </summary>
    public partial class TASK055_GuardRiskHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION risk.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'risk.% % is retained and never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE FUNCTION risk.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'risk.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON risk.risk FOR EACH ROW EXECUTE FUNCTION risk.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON risk.risk_assessment_version FOR EACH ROW EXECUTE FUNCTION risk.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON risk.risk_assessment_impact FOR EACH ROW EXECUTE FUNCTION risk.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON risk.risk_treatment_action FOR EACH ROW EXECUTE FUNCTION risk.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON risk.risk_acceptance FOR EACH ROW EXECUTE FUNCTION risk.refuse_deletion();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON risk.risk FOR EACH STATEMENT EXECUTE FUNCTION risk.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON risk.risk_assessment_version FOR EACH STATEMENT EXECUTE FUNCTION risk.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON risk.risk_assessment_impact FOR EACH STATEMENT EXECUTE FUNCTION risk.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON risk.risk_treatment_action FOR EACH STATEMENT EXECUTE FUNCTION risk.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON risk.risk_acceptance FOR EACH STATEMENT EXECUTE FUNCTION risk.refuse_truncation();

                CREATE FUNCTION risk.guard_risk() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'IDENTIFIED' OR NEW.reopened_count <> 0 OR NEW.materialised_at IS NOT NULL THEN
                            RAISE EXCEPTION 'risk %: a risk is born IDENTIFIED', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_risk_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'risk %: its project never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'CLOSED' AND NEW.status = 'CLOSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'risk % is CLOSED and changes no more until it is reopened', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.materialised_at IS NOT NULL AND NEW.materialised_at IS DISTINCT FROM OLD.materialised_at THEN
                        RAISE EXCEPTION 'risk %: its materialisation is recorded once and never moves', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.reopened_count <> OLD.reopened_count + (CASE WHEN OLD.status = 'CLOSED' AND NEW.status <> 'CLOSED' THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION 'risk %: the reopen count rises by one with each reopen, and only then', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('IDENTIFIED', 'ASSESSED'), ('ASSESSED', 'TREATMENT'), ('MONITORING', 'TREATMENT'), ('ASSESSED', 'MONITORING'),
                        ('TREATMENT', 'MONITORING'), ('MONITORING', 'ASSESSED'), ('IDENTIFIED', 'CLOSED'), ('ASSESSED', 'CLOSED'),
                        ('TREATMENT', 'CLOSED'), ('MONITORING', 'CLOSED'), ('CLOSED', 'IDENTIFIED'), ('CLOSED', 'ASSESSED')) THEN
                        RAISE EXCEPTION 'risk %: % to % is not a step of the risk''s state machine', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_risk BEFORE INSERT OR UPDATE ON risk.risk
                    FOR EACH ROW EXECUTE FUNCTION risk.guard_risk();

                CREATE FUNCTION risk.guard_risk_assessment_version() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'risk_assessment_version % is a recorded assessment and is never changed (APPEND_ONLY)', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.version_no <> COALESCE((SELECT max(v.version_no) FROM risk.risk_assessment_version v WHERE v.risk_id = NEW.risk_id), 0) + 1 THEN
                        RAISE EXCEPTION 'risk_assessment_version %: a version comes next after every earlier version of its risk', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_risk_assessment_version_order';
                    END IF;

                    IF EXISTS (SELECT 1 FROM risk.risk r WHERE r.id = NEW.risk_id AND r.status = 'CLOSED') THEN
                        RAISE EXCEPTION 'risk_assessment_version %: a CLOSED risk is not assessed', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_risk_assessment_version BEFORE INSERT OR UPDATE ON risk.risk_assessment_version
                    FOR EACH ROW EXECUTE FUNCTION risk.guard_risk_assessment_version();

                CREATE FUNCTION risk.guard_risk_assessment_impact() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'risk_assessment_impact % is part of a recorded assessment and is never changed (APPEND_ONLY)', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM risk.risk_assessment_version v
                                   WHERE v.id = NEW.risk_assessment_version_id AND v.created_at = NEW.created_at AND v.created_by = NEW.created_by) THEN
                        RAISE EXCEPTION 'risk_assessment_impact %: an impact is written with its assessment, never added to a recorded one', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_risk_assessment_impact_with_assessment';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_risk_assessment_impact BEFORE INSERT OR UPDATE ON risk.risk_assessment_impact
                    FOR EACH ROW EXECUTE FUNCTION risk.guard_risk_assessment_impact();

                CREATE FUNCTION risk.guard_risk_treatment_action() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'PLANNED' THEN
                            RAISE EXCEPTION 'risk_treatment_action %: an action is born PLANNED', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_risk_treatment_action_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.risk_id, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.risk_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'risk_treatment_action %: its risk never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('COMPLETED', 'CANCELLED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'risk_treatment_action % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('PLANNED', 'IN_PROGRESS'), ('IN_PROGRESS', 'COMPLETED'), ('PLANNED', 'CANCELLED'), ('IN_PROGRESS', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'risk_treatment_action %: % to % is not a step of an action', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_risk_treatment_action BEFORE INSERT OR UPDATE ON risk.risk_treatment_action
                    FOR EACH ROW EXECUTE FUNCTION risk.guard_risk_treatment_action();

                CREATE FUNCTION risk.guard_risk_acceptance() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'ACTIVE' THEN
                            RAISE EXCEPTION 'risk_acceptance %: an acceptance is born ACTIVE', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_risk_acceptance_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF OLD.status <> 'ACTIVE' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'risk_acceptance % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.risk_id, NEW.accepted_by_user_id, NEW.accepted_at, NEW.expires_on, NEW.rationale, NEW.rationale_lang, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.risk_id, OLD.accepted_by_user_id, OLD.accepted_at, OLD.expires_on, OLD.rationale, OLD.rationale_lang, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'risk_acceptance %: an acceptance is never rewritten, and its expiry never extended; a new decision is a new acceptance', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (('ACTIVE', 'EXPIRED'), ('ACTIVE', 'REVOKED')) THEN
                        RAISE EXCEPTION 'risk_acceptance %: % to % is not a step of an acceptance', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_risk_acceptance BEFORE INSERT OR UPDATE ON risk.risk_acceptance
                    FOR EACH ROW EXECUTE FUNCTION risk.guard_risk_acceptance();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_risk_acceptance ON risk.risk_acceptance;
                DROP FUNCTION risk.guard_risk_acceptance();
                DROP TRIGGER guard_risk_treatment_action ON risk.risk_treatment_action;
                DROP FUNCTION risk.guard_risk_treatment_action();
                DROP TRIGGER guard_risk_assessment_impact ON risk.risk_assessment_impact;
                DROP FUNCTION risk.guard_risk_assessment_impact();
                DROP TRIGGER guard_risk_assessment_version ON risk.risk_assessment_version;
                DROP FUNCTION risk.guard_risk_assessment_version();
                DROP TRIGGER guard_risk ON risk.risk;
                DROP FUNCTION risk.guard_risk();
                DROP TRIGGER refuse_truncation ON risk.risk_acceptance;
                DROP TRIGGER refuse_truncation ON risk.risk_treatment_action;
                DROP TRIGGER refuse_truncation ON risk.risk_assessment_impact;
                DROP TRIGGER refuse_truncation ON risk.risk_assessment_version;
                DROP TRIGGER refuse_truncation ON risk.risk;
                DROP TRIGGER refuse_deletion ON risk.risk_acceptance;
                DROP TRIGGER refuse_deletion ON risk.risk_treatment_action;
                DROP TRIGGER refuse_deletion ON risk.risk_assessment_impact;
                DROP TRIGGER refuse_deletion ON risk.risk_assessment_version;
                DROP TRIGGER refuse_deletion ON risk.risk;
                DROP FUNCTION risk.refuse_truncation();
                DROP FUNCTION risk.refuse_deletion();
                """);
        }
    }
}
