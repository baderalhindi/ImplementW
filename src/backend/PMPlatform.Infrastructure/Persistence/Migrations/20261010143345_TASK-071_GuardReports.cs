using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-071, 3 of 3: FG-02's governance and job lifecycle held in the database, whoever writes (FG-02 §11.1, §10.1; BR-RPT-006, -043).
    /// <list type="bullet">
    /// <item>A definition is retained, never deleted or truncated. It is born a DRAFT — the platform seed alone ships PUBLISHED versions — keeps its
    /// code, number and author, and moves only DRAFT → VALIDATED → PUBLISHED and any state but RETIRED → RETIRED. Its content changes only while
    /// DRAFT; a PUBLISHED version changes only by being retired. Its columns, audience, parameters and options are written only while it is a DRAFT
    /// (the seed's own inserts aside). At commit (deferred): one PUBLISHED version per code.</item>
    /// <item>A job is retained. It is born REQUESTED; what was asked, by whom and when never changes; it moves only along REQUESTED → VALIDATING →
    /// QUEUED → RUNNING → COMPLETED → EXPIRED, to FAILED from VALIDATING or RUNNING, and to CANCELLED from any state before COMPLETED; FAILED, CANCELLED
    /// and EXPIRED are final.</item>
    /// <item>An output is retained; what it is never changes, and its status moves only AVAILABLE → EXPIRED → PURGED or AVAILABLE → PURGED. At commit:
    /// an output's job is COMPLETED or EXPIRED — no file is kept for a job that failed or was cancelled — and a PURGED output's bytes are gone.</item>
    /// <item>Stored bytes are never changed, and deleted only with their output's purge (checked at commit).</item>
    /// <item>A saved view keeps its owner and type, and its columns, filters and parameter values stay with it.</item>
    /// </list>
    /// </summary>
    public partial class TASK071_GuardReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION reports.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'reports.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_definition FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_column FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_audience_role FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_parameter FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_parameter_option FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.report_job FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON reports.generated_output FOR EACH STATEMENT EXECUTE FUNCTION reports.refuse_truncation();

                -- The platform seed principal (seed-master-data.sql §1): the one writer that ships PUBLISHED versions.
                CREATE FUNCTION reports.is_seed(writer uuid) RETURNS boolean
                LANGUAGE sql IMMUTABLE AS $$ SELECT writer = '00000000-0000-4000-8000-0000000000ff'::uuid $$;

                CREATE FUNCTION reports.guard_report_definition() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'report_definition % is retained: a version no longer used is retired, never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.lifecycle_state <> 'DRAFT' AND NOT reports.is_seed(NEW.created_by) THEN
                            RAISE EXCEPTION 'report_definition %: a version is born a DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_report_definition_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.code, NEW.version_no, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.code, OLD.version_no, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'report_definition %: its code, number and author never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state = 'RETIRED' THEN
                        RAISE EXCEPTION 'report_definition % is RETIRED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state <> OLD.lifecycle_state AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                        ('DRAFT', 'VALIDATED'), ('VALIDATED', 'PUBLISHED'), ('DRAFT', 'RETIRED'), ('VALIDATED', 'RETIRED'), ('PUBLISHED', 'RETIRED')) THEN
                        RAISE EXCEPTION 'report_definition %: % to % is not a step of the governed lifecycle', OLD.id, OLD.lifecycle_state, NEW.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state <> 'DRAFT'
                       AND (NEW.name_ar, NEW.name_en, NEW.description_ar, NEW.description_en, NEW.audience_family, NEW.primary_projection_code, NEW.allows_saved_views)
                           IS DISTINCT FROM (OLD.name_ar, OLD.name_en, OLD.description_ar, OLD.description_en, OLD.audience_family, OLD.primary_projection_code, OLD.allows_saved_views) THEN
                        RAISE EXCEPTION 'report_definition % is %: its content changed only while DRAFT; a change is a new version', OLD.id, OLD.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state = 'PUBLISHED'
                       AND (NEW.validated_by_user_id, NEW.validated_at, NEW.published_by_user_id, NEW.published_at)
                           IS DISTINCT FROM (OLD.validated_by_user_id, OLD.validated_at, OLD.published_by_user_id, OLD.published_at) THEN
                        RAISE EXCEPTION 'report_definition % is PUBLISHED: only its retirement may change it', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_report_definition BEFORE INSERT OR UPDATE OR DELETE ON reports.report_definition
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_report_definition();

                -- A version's columns, audience and parameters are its content: written only while it is a DRAFT. A parameter's options through it.
                CREATE FUNCTION reports.guard_definition_content() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    row_definition_id uuid;
                    definition_state text;
                BEGIN
                    IF TG_TABLE_NAME = 'report_parameter_option' THEN
                        IF TG_OP = 'UPDATE' AND NEW.report_parameter_id <> OLD.report_parameter_id THEN
                            RAISE EXCEPTION 'reports.% %: a row never moves to another parameter', TG_TABLE_NAME, OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        SELECT p.report_definition_id INTO row_definition_id FROM reports.report_parameter p
                        WHERE p.id = CASE WHEN TG_OP = 'DELETE' THEN OLD.report_parameter_id ELSE NEW.report_parameter_id END;
                    ELSE
                        IF TG_OP = 'UPDATE' AND NEW.report_definition_id <> OLD.report_definition_id THEN
                            RAISE EXCEPTION 'reports.% %: a row never moves to another version', TG_TABLE_NAME, OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        row_definition_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.report_definition_id ELSE NEW.report_definition_id END;
                    END IF;

                    SELECT d.lifecycle_state INTO definition_state FROM reports.report_definition d WHERE d.id = row_definition_id;
                    IF definition_state <> 'DRAFT' AND NOT (TG_OP = 'INSERT' AND reports.is_seed(NEW.created_by)) THEN
                        RAISE EXCEPTION 'reports.%: version % is %, and only a DRAFT''s content is written', TG_TABLE_NAME, row_definition_id, definition_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
                END;
                $$;

                CREATE TRIGGER guard_report_column BEFORE INSERT OR UPDATE OR DELETE ON reports.report_column
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_definition_content();
                CREATE TRIGGER guard_report_audience_role BEFORE INSERT OR UPDATE OR DELETE ON reports.report_audience_role
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_definition_content();
                CREATE TRIGGER guard_report_parameter BEFORE INSERT OR UPDATE OR DELETE ON reports.report_parameter
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_definition_content();
                CREATE TRIGGER guard_report_parameter_option BEFORE INSERT OR UPDATE OR DELETE ON reports.report_parameter_option
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_definition_content();

                -- At commit: one PUBLISHED version per code (ADR-006: ten reports, each in force once).
                CREATE FUNCTION reports.check_published_reports() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (SELECT count(*) FROM reports.report_definition d WHERE d.code = NEW.code AND d.lifecycle_state = 'PUBLISHED') > 1 THEN
                        RAISE EXCEPTION 'report %: one version is PUBLISHED at a time', NEW.code
                            USING ERRCODE = 'unique_violation', CONSTRAINT = 'ck_report_definition_one_published';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_published_reports AFTER INSERT OR UPDATE OF lifecycle_state ON reports.report_definition
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reports.check_published_reports();

                -- A job: born REQUESTED; its request never changes; it moves only along its lifecycle (ERD §6, R-7).
                CREATE FUNCTION reports.guard_report_job() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'report_job % is retained', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'REQUESTED' THEN
                            RAISE EXCEPTION 'report_job %: a job is born REQUESTED', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_report_job_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.kind, NEW.report_definition_id, NEW.requested_by_user_id, NEW.requested_at, NEW.export_format, NEW.report_language,
                        NEW.correlation_id, NEW.idempotency_key, NEW.request_snapshot, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.kind, OLD.report_definition_id, OLD.requested_by_user_id, OLD.requested_at, OLD.export_format, OLD.report_language,
                        OLD.correlation_id, OLD.idempotency_key, OLD.request_snapshot, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'report_job %: what was asked, by whom and when never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status IN ('FAILED', 'CANCELLED', 'EXPIRED') THEN
                        RAISE EXCEPTION 'report_job % is % and changes no more', OLD.id, OLD.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                        ('REQUESTED', 'VALIDATING'), ('VALIDATING', 'QUEUED'), ('QUEUED', 'RUNNING'), ('RUNNING', 'COMPLETED'), ('COMPLETED', 'EXPIRED'),
                        ('VALIDATING', 'FAILED'), ('RUNNING', 'FAILED'),
                        ('REQUESTED', 'CANCELLED'), ('VALIDATING', 'CANCELLED'), ('QUEUED', 'CANCELLED'), ('RUNNING', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'report_job %: % to % is not a step of its lifecycle', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_report_job BEFORE INSERT OR UPDATE OR DELETE ON reports.report_job
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_report_job();

                -- An output: what it is never changes; its status moves forward only.
                CREATE FUNCTION reports.guard_generated_output() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'generated_output % is retained: an expired file is purged, its record kept', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'AVAILABLE' THEN
                            RAISE EXCEPTION 'generated_output %: an output is born AVAILABLE', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_generated_output_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.report_job_id, NEW.storage_object_key, NEW.file_name, NEW.content_type, NEW.size_bytes, NEW.checksum_sha256,
                        NEW.sensitivity, NEW.row_count, NEW.source_as_of, NEW.authorization_footprint, NEW.expires_at, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.report_job_id, OLD.storage_object_key, OLD.file_name, OLD.content_type, OLD.size_bytes, OLD.checksum_sha256,
                        OLD.sensitivity, OLD.row_count, OLD.source_as_of, OLD.authorization_footprint, OLD.expires_at, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'generated_output %: what an output is never changes', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (('AVAILABLE', 'EXPIRED'), ('AVAILABLE', 'PURGED'), ('EXPIRED', 'PURGED')) THEN
                        RAISE EXCEPTION 'generated_output %: % to % is not a step of its lifecycle', OLD.id, OLD.status, NEW.status
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_generated_output BEFORE INSERT OR UPDATE OR DELETE ON reports.generated_output
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_generated_output();

                -- At commit: an output is kept only for a job that completed (BR-RPT-043), and a purged output has no bytes left.
                CREATE FUNCTION reports.check_generated_output() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM reports.report_job j WHERE j.id = NEW.report_job_id AND j.status IN ('COMPLETED', 'EXPIRED')) THEN
                        RAISE EXCEPTION 'generated_output %: an output is kept only for a COMPLETED job', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_generated_output_job_completed';
                    END IF;

                    IF NEW.status = 'PURGED' AND EXISTS (SELECT 1 FROM reports.report_output_content c WHERE c.storage_object_key = NEW.storage_object_key) THEN
                        RAISE EXCEPTION 'generated_output %: a purged output keeps no bytes', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_generated_output_purged_content';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_generated_output AFTER INSERT OR UPDATE ON reports.generated_output
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reports.check_generated_output();

                -- Stored bytes: written once, never changed.
                CREATE FUNCTION reports.guard_report_output_content() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'report_output_content %: stored bytes are never changed', OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER guard_report_output_content BEFORE UPDATE ON reports.report_output_content
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_report_output_content();

                -- At commit: bytes are deleted only with their output's purge.
                CREATE FUNCTION reports.check_report_output_content_deleted() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM reports.generated_output o WHERE o.storage_object_key = OLD.storage_object_key AND o.status <> 'PURGED') THEN
                        RAISE EXCEPTION 'report_output_content %: bytes are deleted only when their output is purged', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_report_output_content_deleted AFTER DELETE ON reports.report_output_content
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reports.check_report_output_content_deleted();

                -- A saved view keeps its owner and type; its columns, filters and parameter values never move to another view.
                CREATE FUNCTION reports.guard_saved_view() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.owner_user_id, NEW.view_type, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.owner_user_id, OLD.view_type, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'saved_view %: its owner and type never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_saved_view BEFORE UPDATE ON reports.saved_view
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_saved_view();

                CREATE FUNCTION reports.guard_saved_view_part() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.saved_view_id <> OLD.saved_view_id THEN
                        RAISE EXCEPTION 'reports.% %: a row never moves to another view', TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_saved_view_column BEFORE UPDATE ON reports.saved_view_column
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_saved_view_part();
                CREATE TRIGGER guard_saved_view_filter BEFORE UPDATE ON reports.saved_view_filter
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_saved_view_part();
                CREATE TRIGGER guard_report_parameter_value BEFORE UPDATE ON reports.report_parameter_value
                    FOR EACH ROW EXECUTE FUNCTION reports.guard_saved_view_part();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER guard_report_parameter_value ON reports.report_parameter_value;
                DROP TRIGGER guard_saved_view_filter ON reports.saved_view_filter;
                DROP TRIGGER guard_saved_view_column ON reports.saved_view_column;
                DROP FUNCTION reports.guard_saved_view_part();
                DROP TRIGGER guard_saved_view ON reports.saved_view;
                DROP FUNCTION reports.guard_saved_view();
                DROP TRIGGER check_report_output_content_deleted ON reports.report_output_content;
                DROP FUNCTION reports.check_report_output_content_deleted();
                DROP TRIGGER guard_report_output_content ON reports.report_output_content;
                DROP FUNCTION reports.guard_report_output_content();
                DROP TRIGGER check_generated_output ON reports.generated_output;
                DROP FUNCTION reports.check_generated_output();
                DROP TRIGGER guard_generated_output ON reports.generated_output;
                DROP FUNCTION reports.guard_generated_output();
                DROP TRIGGER guard_report_job ON reports.report_job;
                DROP FUNCTION reports.guard_report_job();
                DROP TRIGGER check_published_reports ON reports.report_definition;
                DROP FUNCTION reports.check_published_reports();
                DROP TRIGGER guard_report_parameter_option ON reports.report_parameter_option;
                DROP TRIGGER guard_report_parameter ON reports.report_parameter;
                DROP TRIGGER guard_report_audience_role ON reports.report_audience_role;
                DROP TRIGGER guard_report_column ON reports.report_column;
                DROP FUNCTION reports.guard_definition_content();
                DROP TRIGGER guard_report_definition ON reports.report_definition;
                DROP FUNCTION reports.guard_report_definition();
                DROP FUNCTION reports.is_seed(uuid);
                DROP TRIGGER refuse_truncation ON reports.generated_output;
                DROP TRIGGER refuse_truncation ON reports.report_job;
                DROP TRIGGER refuse_truncation ON reports.report_parameter_option;
                DROP TRIGGER refuse_truncation ON reports.report_parameter;
                DROP TRIGGER refuse_truncation ON reports.report_audience_role;
                DROP TRIGGER refuse_truncation ON reports.report_column;
                DROP TRIGGER refuse_truncation ON reports.report_definition;
                DROP FUNCTION reports.refuse_truncation();
                """);
        }
    }
}
