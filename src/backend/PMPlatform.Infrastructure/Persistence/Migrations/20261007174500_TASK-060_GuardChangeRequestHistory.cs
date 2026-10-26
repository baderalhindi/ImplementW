using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-060, 3 of 3: WF-08's history and its acceptance criteria held in the database, whoever writes. A change request is born DRAFT
    /// at revision 1 and never submitted; it keeps its project, type and requester; it moves only along the edges of
    /// <c>ChangeRequestWorkflow</c>; its revision rises by one exactly when a RETURNED request is resubmitted; its fields change only while
    /// it is with its requester; it enters UNDER_REVIEW only with an evaluation of the revision, and IMPLEMENTED only with every
    /// authorisation applied; once REJECTED, WITHDRAWN or CLOSED nothing changes; and only a DRAFT is deleted. An evaluation is appended
    /// while its request is SUBMITTED, one per revision, and never changed. An authorisation is born ISSUED under an APPROVED request whose
    /// WF-11 run approved it; its scope and pinned target never change; it is applied once, by a person, through a record of its target
    /// module, and only while its request is in IMPLEMENTATION — approval alone applies nothing; it is never deleted. No table is truncated.
    /// Reads the <c>change_request</c> schema, and <c>approval.approval_instance</c> to tie an authorisation to the run that approved it.
    /// </summary>
    public partial class TASK060_GuardChangeRequestHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION change_request.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'change_request.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON change_request.change_request FOR EACH STATEMENT EXECUTE FUNCTION change_request.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON change_request.materiality_evaluation FOR EACH STATEMENT EXECUTE FUNCTION change_request.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON change_request.change_authorization FOR EACH STATEMENT EXECUTE FUNCTION change_request.refuse_truncation();

                CREATE FUNCTION change_request.guard_change_request() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'change_request % is %: only a DRAFT never submitted is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' OR NEW.revision_no <> 1 OR NEW.submitted_at IS NOT NULL OR NEW.implemented_at IS NOT NULL OR NEW.closed_at IS NOT NULL THEN
                            RAISE EXCEPTION 'change_request %: a change request is born DRAFT, at revision 1, never submitted', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_change_request_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.project_id, NEW.change_type, NEW.requested_by_user_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.change_type, OLD.requested_by_user_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'change_request %: its project, type and requester never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('REJECTED', 'WITHDRAWN', 'CLOSED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'change_request % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('DRAFT', 'SUBMITTED'), ('RETURNED', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'),
                        ('UNDER_REVIEW', 'APPROVED'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'REJECTED'), ('UNDER_REVIEW', 'WITHDRAWN'),
                        ('SUBMITTED', 'WITHDRAWN'), ('RETURNED', 'WITHDRAWN'),
                        ('APPROVED', 'IMPLEMENTATION'), ('IMPLEMENTATION', 'IMPLEMENTED'), ('IMPLEMENTED', 'CLOSED')) THEN
                        RAISE EXCEPTION 'change_request %: % to % is not a step of the change request''s state machine', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.revision_no <> OLD.revision_no + (CASE WHEN OLD.status = 'RETURNED' AND NEW.status = 'SUBMITTED' THEN 1 ELSE 0 END) THEN
                        RAISE EXCEPTION 'change_request %: the revision rises by one when a returned request is resubmitted, and only then', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status NOT IN ('DRAFT', 'RETURNED')
                       AND (NEW.title, NEW.title_lang, NEW.justification, NEW.justification_lang, NEW.cost_impact_sar, NEW.schedule_impact_days,
                            NEW.scope_impact, NEW.scope_impact_lang, NEW.is_contractual_obligation, NEW.requested_governance_profile_item_id)
                           IS DISTINCT FROM
                           (OLD.title, OLD.title_lang, OLD.justification, OLD.justification_lang, OLD.cost_impact_sar, OLD.schedule_impact_days,
                            OLD.scope_impact, OLD.scope_impact_lang, OLD.is_contractual_obligation, OLD.requested_governance_profile_item_id) THEN
                        RAISE EXCEPTION 'change_request % is %: its fields are those it was submitted with', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.submitted_at IS DISTINCT FROM OLD.submitted_at AND NOT (NEW.status = 'SUBMITTED' AND OLD.status IN ('DRAFT', 'RETURNED')) THEN
                        RAISE EXCEPTION 'change_request %: it is submitted only by its submission', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.implemented_at IS NOT NULL AND NEW.implemented_at IS DISTINCT FROM OLD.implemented_at THEN
                        RAISE EXCEPTION 'change_request %: when it was implemented never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'SUBMITTED' AND NEW.status = 'UNDER_REVIEW'
                       AND NOT EXISTS (SELECT 1 FROM change_request.materiality_evaluation e WHERE e.change_request_id = NEW.id AND e.revision_no = NEW.revision_no) THEN
                        RAISE EXCEPTION 'change_request %: its review starts with the materiality evaluation of revision %', OLD.id, NEW.revision_no
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'IMPLEMENTATION' AND NEW.status = 'IMPLEMENTED'
                       AND EXISTS (SELECT 1 FROM change_request.change_authorization a WHERE a.change_request_id = NEW.id AND a.status <> 'APPLIED') THEN
                        RAISE EXCEPTION 'change_request %: a change is implemented once each of its authorisations is applied by its target module', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_change_request BEFORE INSERT OR UPDATE OR DELETE ON change_request.change_request
                    FOR EACH ROW EXECUTE FUNCTION change_request.guard_change_request();

                CREATE FUNCTION change_request.guard_materiality_evaluation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP <> 'INSERT' THEN
                        RAISE EXCEPTION 'materiality_evaluation %: an evaluation is appended, never changed or deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM change_request.change_request r
                                   WHERE r.id = NEW.change_request_id AND r.status = 'SUBMITTED' AND r.revision_no = NEW.revision_no) THEN
                        RAISE EXCEPTION 'materiality_evaluation %: a revision is evaluated as its review starts, while it is SUBMITTED', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_materiality_evaluation BEFORE INSERT OR UPDATE OR DELETE ON change_request.materiality_evaluation
                    FOR EACH ROW EXECUTE FUNCTION change_request.guard_materiality_evaluation();

                CREATE FUNCTION change_request.guard_change_authorization() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'change_authorization % is retained and never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'ISSUED' THEN
                            RAISE EXCEPTION 'change_authorization %: an authorisation is born ISSUED', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_change_authorization_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM change_request.change_request r WHERE r.id = NEW.change_request_id AND r.status = 'APPROVED')
                           OR NOT EXISTS (SELECT 1 FROM approval.approval_instance i
                                          WHERE i.id = NEW.approval_instance_id AND i.subject_module = 'ChangeRequest' AND i.subject_id = NEW.change_request_id
                                            AND i.status = 'APPROVED') THEN
                            RAISE EXCEPTION 'change_authorization %: it is issued by the approval of its change request''s WF-11 run', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.change_request_id, NEW.approval_instance_id, NEW.authorization_scope, NEW.target_module, NEW.target_type, NEW.target_id,
                        NEW.target_revision_no, NEW.idempotency_key, NEW.issued_at, NEW.expires_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.change_request_id, OLD.approval_instance_id, OLD.authorization_scope, OLD.target_module, OLD.target_type,
                        OLD.target_id, OLD.target_revision_no, OLD.idempotency_key, OLD.issued_at, OLD.expires_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'change_authorization %: its scope and pinned target never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'ISSUED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'change_authorization % is % and changes no more: an authorisation is applied once', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'APPLIED' AND OLD.status = 'ISSUED'
                       AND NOT EXISTS (SELECT 1 FROM change_request.change_request r WHERE r.id = NEW.change_request_id AND r.status = 'IMPLEMENTATION') THEN
                        RAISE EXCEPTION 'change_authorization %: it is applied while its change request is being implemented, never by the approval itself', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_change_authorization BEFORE INSERT OR UPDATE OR DELETE ON change_request.change_authorization
                    FOR EACH ROW EXECUTE FUNCTION change_request.guard_change_authorization();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_change_authorization ON change_request.change_authorization;
                DROP FUNCTION change_request.guard_change_authorization();
                DROP TRIGGER guard_materiality_evaluation ON change_request.materiality_evaluation;
                DROP FUNCTION change_request.guard_materiality_evaluation();
                DROP TRIGGER guard_change_request ON change_request.change_request;
                DROP FUNCTION change_request.guard_change_request();
                DROP TRIGGER refuse_truncation ON change_request.change_authorization;
                DROP TRIGGER refuse_truncation ON change_request.materiality_evaluation;
                DROP TRIGGER refuse_truncation ON change_request.change_request;
                DROP FUNCTION change_request.refuse_truncation();
                """);
        }
    }
}
