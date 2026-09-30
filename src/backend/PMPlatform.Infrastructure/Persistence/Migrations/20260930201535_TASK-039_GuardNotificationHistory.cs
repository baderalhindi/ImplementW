using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-039, 3 of 3: notification history is fixed in the database, whoever writes to it. No template, intent, parameter
    /// or delivery is ever deleted (RETAIN). A template is born DRAFT, moves only forward through the governed lifecycle, and
    /// its text changes only while DRAFT; its event type, channel and number never. An intent's source, scope, schedule and
    /// link never change; SUPPRESSED is final, and FAILED returns only to RECEIVED (a redrive). A parameter never changes. A
    /// delivery's recipient, channel, template and rendered content never change: history shows what was sent. A delivery is
    /// born PENDING (e-mail, SMS), SENT (in-app) or SUPPRESSED, moves only along the transitions the runtime makes, and
    /// DELIVERED, READ and SUPPRESSED are final.
    /// </summary>
    public partial class TASK039_GuardNotificationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION notifications.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'notifications.% % is notification history and is never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON notifications.notification_template
                    FOR EACH ROW EXECUTE FUNCTION notifications.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON notifications.notification_intent
                    FOR EACH ROW EXECUTE FUNCTION notifications.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON notifications.notification_intent_parameter
                    FOR EACH ROW EXECUTE FUNCTION notifications.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON notifications.notification_delivery
                    FOR EACH ROW EXECUTE FUNCTION notifications.refuse_deletion();

                CREATE FUNCTION notifications.guard_notification_template() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.lifecycle_state <> 'DRAFT' THEN
                            RAISE EXCEPTION 'notification_template %: a template is born DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_notification_template_born_draft';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.event_family_code, NEW.event_type, NEW.channel, NEW.version_no, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.event_family_code, OLD.event_type, OLD.channel, OLD.version_no, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'notification_template %: its event type, channel and version never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state = 'RETIRED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'notification_template % is RETIRED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state <> 'DRAFT'
                       AND (NEW.subject_ar, NEW.subject_en, NEW.body_ar, NEW.body_en) IS DISTINCT FROM (OLD.subject_ar, OLD.subject_en, OLD.body_ar, OLD.body_en) THEN
                        RAISE EXCEPTION 'notification_template % is %: its text changed only while DRAFT; a new wording is a new version', OLD.id, OLD.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state IS DISTINCT FROM OLD.lifecycle_state
                       AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN
                           (('DRAFT', 'VALIDATED'), ('DRAFT', 'RETIRED'), ('VALIDATED', 'PUBLISHED'), ('VALIDATED', 'RETIRED'), ('PUBLISHED', 'RETIRED')) THEN
                        RAISE EXCEPTION 'notification_template %: % to % is not a lifecycle step', OLD.id, OLD.lifecycle_state, NEW.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_notification_template
                    BEFORE INSERT OR UPDATE ON notifications.notification_template
                    FOR EACH ROW EXECUTE FUNCTION notifications.guard_notification_template();

                CREATE FUNCTION notifications.guard_notification_intent() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (to_jsonb(NEW) - ARRAY['status', 'suppression_reason', 'condition_revalidated_at', 'updated_at', 'updated_by'])
                       IS DISTINCT FROM
                       (to_jsonb(OLD) - ARRAY['status', 'suppression_reason', 'condition_revalidated_at', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'notification_intent %: what the source sent never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'SUPPRESSED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'notification_intent % is SUPPRESSED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status
                       AND (OLD.status, NEW.status) NOT IN
                           (('RECEIVED', 'ROUTED'), ('RECEIVED', 'COMPLETED'), ('RECEIVED', 'SUPPRESSED'), ('RECEIVED', 'FAILED'),
                            ('SCHEDULED', 'ROUTED'), ('SCHEDULED', 'COMPLETED'), ('SCHEDULED', 'SUPPRESSED'), ('SCHEDULED', 'FAILED'),
                            ('ROUTED', 'COMPLETED'), ('COMPLETED', 'ROUTED'), ('FAILED', 'RECEIVED')) THEN
                        RAISE EXCEPTION 'notification_intent %: % to % is not a step of its routing', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_notification_intent
                    BEFORE UPDATE ON notifications.notification_intent
                    FOR EACH ROW EXECUTE FUNCTION notifications.guard_notification_intent();

                CREATE FUNCTION notifications.refuse_parameter_change() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'notification_intent_parameter %: a parameter the source sent never changes', OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_parameter_change
                    BEFORE UPDATE ON notifications.notification_intent_parameter
                    FOR EACH ROW EXECUTE FUNCTION notifications.refuse_parameter_change();

                CREATE FUNCTION notifications.guard_notification_delivery() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NOT (NEW.status = 'SUPPRESSED'
                                OR (NEW.channel = 'IN_APP' AND NEW.status = 'SENT')
                                OR (NEW.channel <> 'IN_APP' AND NEW.status = 'PENDING')) THEN
                            RAISE EXCEPTION 'notification_delivery %: a % delivery is not born %', NEW.id, NEW.channel, NEW.status
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_notification_delivery_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.notification_intent_id, NEW.recipient_user_id, NEW.channel, NEW.notification_template_id,
                        NEW.rendered_language, NEW.rendered_subject, NEW.rendered_body, NEW.segment_count, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.notification_intent_id, OLD.recipient_user_id, OLD.channel, OLD.notification_template_id,
                        OLD.rendered_language, OLD.rendered_subject, OLD.rendered_body, OLD.segment_count, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'notification_delivery %: its recipient, channel and rendered content never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('DELIVERED', 'READ', 'SUPPRESSED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'notification_delivery % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status
                       AND (OLD.status, NEW.status) NOT IN
                           (('PENDING', 'SENT'), ('PENDING', 'FAILED'), ('PENDING', 'DEAD_LETTER'), ('PENDING', 'SUPPRESSED'),
                            ('FAILED', 'SENT'), ('FAILED', 'DEAD_LETTER'), ('FAILED', 'SUPPRESSED'),
                            ('SENT', 'DELIVERED'), ('SENT', 'READ'), ('SENT', 'FAILED'),
                            ('DEAD_LETTER', 'PENDING')) THEN
                        RAISE EXCEPTION 'notification_delivery %: % to % is not a step of its delivery', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_notification_delivery
                    BEFORE INSERT OR UPDATE ON notifications.notification_delivery
                    FOR EACH ROW EXECUTE FUNCTION notifications.guard_notification_delivery();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_notification_delivery ON notifications.notification_delivery;
                DROP FUNCTION notifications.guard_notification_delivery();
                DROP TRIGGER refuse_parameter_change ON notifications.notification_intent_parameter;
                DROP FUNCTION notifications.refuse_parameter_change();
                DROP TRIGGER guard_notification_intent ON notifications.notification_intent;
                DROP FUNCTION notifications.guard_notification_intent();
                DROP TRIGGER guard_notification_template ON notifications.notification_template;
                DROP FUNCTION notifications.guard_notification_template();
                DROP TRIGGER refuse_deletion ON notifications.notification_delivery;
                DROP TRIGGER refuse_deletion ON notifications.notification_intent_parameter;
                DROP TRIGGER refuse_deletion ON notifications.notification_intent;
                DROP TRIGGER refuse_deletion ON notifications.notification_template;
                DROP FUNCTION notifications.refuse_deletion();
                """);
        }
    }
}
