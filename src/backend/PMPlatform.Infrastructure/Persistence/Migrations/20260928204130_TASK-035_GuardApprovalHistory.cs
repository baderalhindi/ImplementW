using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-035, 4 of 4: approval history is fixed in the database, whoever writes to it. No approval row is ever deleted
    /// (RETAIN). A run's subject, route and requester never change; a decided run changes only once more, when its
    /// outcome is marked delivered. A decided task, and a revoked or expired delegation, change no more. And ADR-013 — no
    /// external approval authority of any kind — is refused here as well as in the application: an external user is
    /// never the decider or the authority of an approval task, nor either side of a delegation.
    /// </summary>
    public partial class TASK035_GuardApprovalHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION approval.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'approval.% % is approval history and is never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                -- ADR-013: the user may hold approval authority only if internal. NULL (no one yet) passes.
                CREATE FUNCTION approval.require_internal_user(user_id uuid, role text) RETURNS void
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF user_id IS NOT NULL AND NOT EXISTS (
                        SELECT 1 FROM identity_access."user" u WHERE u.id = user_id AND u.user_type = 'INTERNAL') THEN
                        RAISE EXCEPTION 'ADR-013: user % is not internal and holds no approval authority (%)', user_id, role
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_approval_internal_authority';
                    END IF;
                END;
                $$;

                CREATE FUNCTION approval.guard_approval_instance() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (to_jsonb(NEW) - ARRAY['status', 'completed_at', 'outcome_delivered_at', 'updated_at', 'updated_by'])
                       IS DISTINCT FROM
                       (to_jsonb(OLD) - ARRAY['status', 'completed_at', 'outcome_delivered_at', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'approval_instance %: its subject, route and requester never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status <> 'PENDING' AND (
                        (NEW.status, NEW.completed_at) IS DISTINCT FROM (OLD.status, OLD.completed_at)
                        OR (OLD.outcome_delivered_at IS NOT NULL AND NEW.outcome_delivered_at IS DISTINCT FROM OLD.outcome_delivered_at)) THEN
                        RAISE EXCEPTION 'approval_instance % is %: only its outcome delivery may still be recorded', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.outcome_delivered_at IS NOT NULL AND NEW.status = 'PENDING' THEN
                        RAISE EXCEPTION 'approval_instance % is PENDING and has no outcome to deliver', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_approval_instance
                    BEFORE UPDATE ON approval.approval_instance
                    FOR EACH ROW EXECUTE FUNCTION approval.guard_approval_instance();

                CREATE FUNCTION approval.guard_approval_task() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD.status <> 'PENDING' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                            RAISE EXCEPTION 'approval_task % is %: a decided task never changes', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF (NEW.approval_instance_id, NEW.sequence_no, NEW.assigned_role_id, NEW.created_at, NEW.created_by)
                           IS DISTINCT FROM
                           (OLD.approval_instance_id, OLD.sequence_no, OLD.assigned_role_id, OLD.created_at, OLD.created_by) THEN
                            RAISE EXCEPTION 'approval_task %: its run, stage and role never change', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    END IF;

                    PERFORM approval.require_internal_user(NEW.acting_user_id, 'acting user');
                    PERFORM approval.require_internal_user(NEW.assigned_user_id, 'assigned user');
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_approval_task
                    BEFORE INSERT OR UPDATE ON approval.approval_task
                    FOR EACH ROW EXECUTE FUNCTION approval.guard_approval_task();

                CREATE FUNCTION approval.guard_approval_delegation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND (
                        OLD.status <> 'ACTIVE'
                        OR (to_jsonb(NEW) - ARRAY['status', 'revoked_at', 'updated_at', 'updated_by'])
                           IS DISTINCT FROM
                           (to_jsonb(OLD) - ARRAY['status', 'revoked_at', 'updated_at', 'updated_by'])) THEN
                        RAISE EXCEPTION 'approval_delegation %: only an ACTIVE delegation changes, and only by its revocation or expiry', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    PERFORM approval.require_internal_user(NEW.delegator_user_id, 'delegator');
                    PERFORM approval.require_internal_user(NEW.delegate_user_id, 'delegate');
                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_approval_delegation
                    BEFORE INSERT OR UPDATE ON approval.approval_delegation
                    FOR EACH ROW EXECUTE FUNCTION approval.guard_approval_delegation();

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON approval.approval_instance FOR EACH ROW EXECUTE FUNCTION approval.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON approval.approval_task FOR EACH ROW EXECUTE FUNCTION approval.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON approval.approval_delegation FOR EACH ROW EXECUTE FUNCTION approval.refuse_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER refuse_deletion ON approval.approval_delegation;
                DROP TRIGGER refuse_deletion ON approval.approval_task;
                DROP TRIGGER refuse_deletion ON approval.approval_instance;
                DROP TRIGGER guard_approval_delegation ON approval.approval_delegation;
                DROP FUNCTION approval.guard_approval_delegation();
                DROP TRIGGER guard_approval_task ON approval.approval_task;
                DROP FUNCTION approval.guard_approval_task();
                DROP TRIGGER guard_approval_instance ON approval.approval_instance;
                DROP FUNCTION approval.guard_approval_instance();
                DROP FUNCTION approval.require_internal_user(uuid, text);
                DROP FUNCTION approval.refuse_deletion();
                """);
        }
    }
}
