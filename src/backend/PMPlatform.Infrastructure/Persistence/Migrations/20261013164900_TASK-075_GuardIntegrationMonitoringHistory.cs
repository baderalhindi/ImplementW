using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-075: FG-05's history is fixed in the database. No row of integration_monitoring is deleted (RETAIN). An attempt or a run
    /// changes only until it ends: a retry is a new attempt of the same lineage, never a rewrite of the failed one (INT-CC-13); the
    /// attempt it follows only stops being the lineage's pending retry or dead letter. An alert is born OPEN, keeps what it reported,
    /// moves only forward, and RESOLVED and DISMISSED are final.
    /// </summary>
    public partial class TASK075_GuardIntegrationMonitoringHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION integration_monitoring.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'integration_monitoring.% % is integration monitoring history and is never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON integration_monitoring.integration_definition
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON integration_monitoring.integration_instance
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON integration_monitoring.sync_run
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON integration_monitoring.invocation
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON integration_monitoring.operational_alert
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.refuse_deletion();

                CREATE FUNCTION integration_monitoring.guard_invocation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    -- The one change to an ended attempt: once its lineage's next attempt exists, a RETRY_SCHEDULED or DEAD_LETTER
                    -- attempt is plainly FAILED. What it failed with, and when, stays as it was.
                    IF OLD.status IN ('RETRY_SCHEDULED', 'DEAD_LETTER') AND NEW.status = 'FAILED' AND NEW.next_attempt_at IS NULL
                       AND (to_jsonb(NEW) - ARRAY['status', 'next_attempt_at', 'updated_at', 'updated_by'])
                           = (to_jsonb(OLD) - ARRAY['status', 'next_attempt_at', 'updated_at', 'updated_by']) THEN
                        RETURN NEW;
                    END IF;

                    IF OLD.status <> 'IN_PROGRESS' THEN
                        RAISE EXCEPTION 'invocation % ended % and changes no more: a retry is a new attempt', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.integration_instance_id, NEW.sync_run_id, NEW.operation_code, NEW.correlation_id, NEW.idempotency_key, NEW.attempt_no,
                        NEW.source_module, NEW.source_reference, NEW.started_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.integration_instance_id, OLD.sync_run_id, OLD.operation_code, OLD.correlation_id, OLD.idempotency_key, OLD.attempt_no,
                        OLD.source_module, OLD.source_reference, OLD.started_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'invocation %: its lineage, attempt number and correlation never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_invocation
                    BEFORE UPDATE ON integration_monitoring.invocation
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.guard_invocation();

                CREATE FUNCTION integration_monitoring.guard_sync_run() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.status <> 'RUNNING' THEN
                        RAISE EXCEPTION 'sync_run % ended % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF (NEW.id, NEW.integration_instance_id, NEW.run_type, NEW.correlation_id, NEW.started_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.integration_instance_id, OLD.run_type, OLD.correlation_id, OLD.started_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'sync_run %: its instance, type and correlation never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_sync_run
                    BEFORE UPDATE ON integration_monitoring.sync_run
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.guard_sync_run();

                CREATE FUNCTION integration_monitoring.guard_operational_alert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'OPEN' THEN
                            RAISE EXCEPTION 'operational_alert %: an alert is born OPEN', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_operational_alert_born_open';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.integration_instance_id, NEW.sync_run_id, NEW.invocation_id, NEW.alert_type, NEW.severity, NEW.raised_at, NEW.detail,
                        NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.integration_instance_id, OLD.sync_run_id, OLD.invocation_id, OLD.alert_type, OLD.severity, OLD.raised_at, OLD.detail,
                        OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'operational_alert %: what it reported never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('RESOLVED', 'DISMISSED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'operational_alert % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status IS DISTINCT FROM OLD.status
                       AND (OLD.status, NEW.status) NOT IN
                           (('OPEN', 'ACKNOWLEDGED'), ('OPEN', 'RESOLVED'), ('OPEN', 'DISMISSED'), ('ACKNOWLEDGED', 'RESOLVED'), ('ACKNOWLEDGED', 'DISMISSED')) THEN
                        RAISE EXCEPTION 'operational_alert %: % to % is not a step of its lifecycle', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_operational_alert
                    BEFORE INSERT OR UPDATE ON integration_monitoring.operational_alert
                    FOR EACH ROW EXECUTE FUNCTION integration_monitoring.guard_operational_alert();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_operational_alert ON integration_monitoring.operational_alert;
                DROP FUNCTION integration_monitoring.guard_operational_alert();
                DROP TRIGGER guard_sync_run ON integration_monitoring.sync_run;
                DROP FUNCTION integration_monitoring.guard_sync_run();
                DROP TRIGGER guard_invocation ON integration_monitoring.invocation;
                DROP FUNCTION integration_monitoring.guard_invocation();
                DROP TRIGGER refuse_deletion ON integration_monitoring.operational_alert;
                DROP TRIGGER refuse_deletion ON integration_monitoring.invocation;
                DROP TRIGGER refuse_deletion ON integration_monitoring.sync_run;
                DROP TRIGGER refuse_deletion ON integration_monitoring.integration_instance;
                DROP TRIGGER refuse_deletion ON integration_monitoring.integration_definition;
                DROP FUNCTION integration_monitoring.refuse_deletion();
                """);
        }
    }
}
