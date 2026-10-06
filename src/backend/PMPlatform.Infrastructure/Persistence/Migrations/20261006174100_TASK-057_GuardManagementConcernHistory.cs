using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-057, 3 of 3: WF-07's history held in the database, whoever writes. A concern is born OPEN at revision 1, unassigned and
    /// unresolved; it keeps its project, type, raiser and origin; it moves only along the edges of <c>ConcernWorkflow</c>; its revision
    /// rises by one exactly when a validation returns it; from PENDING_VALIDATION its fields, impacts and severity are fixed, and once
    /// CLOSED nothing changes. An impact is replaced, never rewritten. An escalation is born OPEN, numbered next within its concern, by
    /// an internal user (ADR-013); it ends once — resolved by an internal user, or withdrawn by its escalator — and is never rewritten.
    /// Nothing but an impact is deleted, and no table is truncated. Reads the <c>management_concern</c> schema, and
    /// <c>identity_access.user</c> for the internal-user rule only, as WF-11's <c>ck_approval_internal_authority</c> does.
    /// </summary>
    public partial class TASK057_GuardManagementConcernHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION management_concern.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'management_concern.% % is retained and never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE FUNCTION management_concern.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'management_concern.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON management_concern.management_concern FOR EACH ROW EXECUTE FUNCTION management_concern.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON management_concern.concern_escalation FOR EACH ROW EXECUTE FUNCTION management_concern.refuse_deletion();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON management_concern.management_concern FOR EACH STATEMENT EXECUTE FUNCTION management_concern.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON management_concern.concern_impact FOR EACH STATEMENT EXECUTE FUNCTION management_concern.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON management_concern.concern_escalation FOR EACH STATEMENT EXECUTE FUNCTION management_concern.refuse_truncation();

                CREATE FUNCTION management_concern.require_internal_user(user_id uuid, role text) RETURNS void
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM identity_access."user" u WHERE u.id = user_id AND u.user_type = 'INTERNAL') THEN
                        RAISE EXCEPTION 'ADR-013: user % is not internal and does not % a concern escalation', user_id, role
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_concern_escalation_internal';
                    END IF;
                END;
                $$;

                CREATE FUNCTION management_concern.guard_management_concern() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'OPEN' OR NEW.revision_no <> 1 OR NEW.assignee_user_id IS NOT NULL OR NEW.resolution IS NOT NULL
                           OR NEW.last_reviewed_at IS NOT NULL THEN
                            RAISE EXCEPTION 'management_concern %: a concern is born OPEN, at revision 1, unassigned and unresolved', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_management_concern_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.concern_type, NEW.raised_by_user_id, NEW.raised_at, NEW.originating_risk_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.concern_type, OLD.raised_by_user_id, OLD.raised_at, OLD.originating_risk_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'management_concern %: its project, type, raiser and origin never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'CLOSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'management_concern % is CLOSED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('OPEN', 'ASSIGNED'), ('ASSIGNED', 'IN_PROGRESS'), ('IN_PROGRESS', 'PENDING_VALIDATION'),
                        ('PENDING_VALIDATION', 'RESOLVED'), ('PENDING_VALIDATION', 'IN_PROGRESS'), ('RESOLVED', 'CLOSED')) THEN
                        RAISE EXCEPTION 'management_concern %: % to % is not a step of the concern''s state machine', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no <> OLD.revision_no + (CASE WHEN OLD.status = 'PENDING_VALIDATION' AND NEW.status = 'IN_PROGRESS' THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION 'management_concern %: the revision rises by one when a validation returns the concern, and only then', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('PENDING_VALIDATION', 'RESOLVED')
                       AND (to_jsonb(NEW) - ARRAY['status', 'revision_no', 'resolved_at', 'closed_at', 'next_review_date', 'last_reviewed_at', 'updated_at', 'updated_by'])
                           IS DISTINCT FROM
                           (to_jsonb(OLD) - ARRAY['status', 'revision_no', 'resolved_at', 'closed_at', 'next_review_date', 'last_reviewed_at', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'management_concern % is %: its fields and severity are those its resolution was submitted with', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_management_concern BEFORE INSERT OR UPDATE ON management_concern.management_concern
                    FOR EACH ROW EXECUTE FUNCTION management_concern.guard_management_concern();

                CREATE FUNCTION management_concern.guard_concern_impact() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    concern_id uuid := CASE WHEN TG_OP = 'DELETE' THEN OLD.management_concern_id ELSE NEW.management_concern_id END;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        RAISE EXCEPTION 'concern_impact %: an impact is replaced by a reassessment, never rewritten', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM management_concern.management_concern c
                                   WHERE c.id = concern_id AND c.status IN ('OPEN', 'ASSIGNED', 'IN_PROGRESS')) THEN
                        RAISE EXCEPTION 'concern_impact: the impacts of concern % are fixed once its resolution is submitted', concern_id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
                END;
                $$;

                CREATE TRIGGER guard_concern_impact BEFORE INSERT OR UPDATE OR DELETE ON management_concern.concern_impact
                    FOR EACH ROW EXECUTE FUNCTION management_concern.guard_concern_impact();

                CREATE FUNCTION management_concern.guard_concern_escalation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'OPEN' THEN
                            RAISE EXCEPTION 'concern_escalation %: an escalation is born OPEN', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_concern_escalation_born';
                        END IF;

                        IF NEW.escalation_no <> COALESCE((SELECT max(e.escalation_no) FROM management_concern.concern_escalation e
                                                         WHERE e.management_concern_id = NEW.management_concern_id), 0) + 1 THEN
                            RAISE EXCEPTION 'concern_escalation %: an escalation is numbered next within its concern', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_concern_escalation_order';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM management_concern.management_concern c
                                       WHERE c.id = NEW.management_concern_id AND c.status IN ('OPEN', 'ASSIGNED', 'IN_PROGRESS', 'PENDING_VALIDATION')) THEN
                            RAISE EXCEPTION 'concern_escalation %: a RESOLVED or CLOSED concern is not escalated', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        PERFORM management_concern.require_internal_user(NEW.escalated_by_user_id, 'raise');
                        RETURN NEW;
                    END IF;

                    IF OLD.status <> 'OPEN' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'concern_escalation % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.management_concern_id, NEW.escalation_no, NEW.escalated_by_user_id, NEW.escalated_at, NEW.escalated_to_role_id,
                        NEW.reason, NEW.reason_lang, NEW.request_key, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.management_concern_id, OLD.escalation_no, OLD.escalated_by_user_id, OLD.escalated_at, OLD.escalated_to_role_id,
                        OLD.reason, OLD.reason_lang, OLD.request_key, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'concern_escalation %: an escalation is never rewritten', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (('OPEN', 'RESOLVED'), ('OPEN', 'WITHDRAWN')) THEN
                        RAISE EXCEPTION 'concern_escalation %: % to % is not a step of an escalation', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'WITHDRAWN' AND OLD.status = 'OPEN' AND NEW.resolved_by_user_id IS DISTINCT FROM NEW.escalated_by_user_id THEN
                        RAISE EXCEPTION 'concern_escalation %: only its escalator withdraws an escalation', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'RESOLVED' AND OLD.status = 'OPEN' THEN
                        PERFORM management_concern.require_internal_user(NEW.resolved_by_user_id, 'resolve');
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_concern_escalation BEFORE INSERT OR UPDATE ON management_concern.concern_escalation
                    FOR EACH ROW EXECUTE FUNCTION management_concern.guard_concern_escalation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_concern_escalation ON management_concern.concern_escalation;
                DROP FUNCTION management_concern.guard_concern_escalation();
                DROP TRIGGER guard_concern_impact ON management_concern.concern_impact;
                DROP FUNCTION management_concern.guard_concern_impact();
                DROP TRIGGER guard_management_concern ON management_concern.management_concern;
                DROP FUNCTION management_concern.guard_management_concern();
                DROP FUNCTION management_concern.require_internal_user(uuid, text);
                DROP TRIGGER refuse_truncation ON management_concern.concern_escalation;
                DROP TRIGGER refuse_truncation ON management_concern.concern_impact;
                DROP TRIGGER refuse_truncation ON management_concern.management_concern;
                DROP TRIGGER refuse_deletion ON management_concern.concern_escalation;
                DROP TRIGGER refuse_deletion ON management_concern.management_concern;
                DROP FUNCTION management_concern.refuse_truncation();
                DROP FUNCTION management_concern.refuse_deletion();
                """);
        }
    }
}
