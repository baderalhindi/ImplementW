using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-066, 3 of 3: WF-13's history and its acceptance criteria held in the database, whoever writes.
    /// <list type="bullet">
    /// <item>A request is born DRAFT and AHDA-issued for a project in a state that admits one; keeps its project, entity and origin; moves only
    /// along its edges; changes its purpose, source and due date only while DRAFT; is issued once; names as responder only an EXTERNAL user of
    /// its own entity and as reviewer only an INTERNAL user; once CLOSED or CANCELLED nothing changes; only a DRAFT is deleted.</item>
    /// <item>A revision is born DRAFT, of an issued request, with the request's project and entity, numbered one after the RETURNED revision it
    /// corrects; is answered by an EXTERNAL user of its entity and decided by an INTERNAL one; moves only along its edges; once submitted keeps
    /// what was submitted and the source version it was answered against; is decided once; once final nothing changes; is never deleted.</item>
    /// <item>A revision's values are written only while it is a DRAFT of a request that is not final — the reviewer's "no silent edit"
    /// (acceptance criterion 2) for any writer.</item>
    /// <item>An application attempt is never deleted and never changed, but by the one revalidation of a CONFLICT.</item>
    /// </list>
    /// At commit (deferred constraint triggers, so the order of one transaction's writes does not matter): a request has at most one revision
    /// that is not final, and its status agrees with its revisions; an APPLIED attempt is of a revision that is APPLIED, and a revision of a
    /// source record is APPLIED only with its APPLIED attempt and APPLICATION_FAILED only with a FAILED one. No table is truncated. Reads
    /// <c>project.project</c> and <c>identity_access."user"</c>.
    /// </summary>
    public partial class TASK066_GuardExternalParticipationHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION external_participation.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'external_participation.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON external_participation.external_update_request FOR EACH STATEMENT EXECUTE FUNCTION external_participation.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON external_participation.external_contribution FOR EACH STATEMENT EXECUTE FUNCTION external_participation.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON external_participation.external_contribution_field FOR EACH STATEMENT EXECUTE FUNCTION external_participation.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON external_participation.source_application FOR EACH STATEMENT EXECUTE FUNCTION external_participation.refuse_truncation();

                -- ADR-013: the people of a request are the entity's on its side and AHDA's on the other.
                CREATE FUNCTION external_participation.is_external_user_of(checked_user_id uuid, checked_entity_id uuid) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (SELECT 1 FROM identity_access."user" u
                                   WHERE u.id = checked_user_id AND u.user_type = 'EXTERNAL' AND u.external_entity_id = checked_entity_id)
                $$;

                CREATE FUNCTION external_participation.is_internal_user(checked_user_id uuid) RETURNS boolean
                LANGUAGE sql STABLE AS $$
                    SELECT EXISTS (SELECT 1 FROM identity_access."user" u WHERE u.id = checked_user_id AND u.user_type = 'INTERNAL')
                $$;

                CREATE FUNCTION external_participation.guard_external_update_request() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.status <> 'DRAFT' THEN
                            RAISE EXCEPTION 'external_update_request % is %: only a DRAFT is deleted (HARD_DRAFT)', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.status <> 'DRAFT' OR NEW.origin <> 'AHDA_ISSUED' THEN
                            RAISE EXCEPTION 'external_update_request %: a request is born a DRAFT of AHDA''s', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_external_update_request_born';
                        END IF;

                        IF NOT EXISTS (SELECT 1 FROM project.project p WHERE p.id = NEW.project_id
                                       AND p.lifecycle_state IN ('APPROVED_PLANNED', 'ACTIVE', 'SUSPENDED')) THEN
                            RAISE EXCEPTION 'external_update_request %: a request is drafted for an APPROVED_PLANNED, ACTIVE or SUSPENDED project', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    ELSE
                        IF (NEW.id, NEW.project_id, NEW.external_entity_id, NEW.origin, NEW.created_at, NEW.created_by)
                           IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.external_entity_id, OLD.origin, OLD.created_at, OLD.created_by) THEN
                            RAISE EXCEPTION 'external_update_request %: its project, entity and origin never change', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.status IN ('CLOSED', 'CANCELLED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                            RAISE EXCEPTION 'external_update_request % is % and changes no more', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                            ('DRAFT', 'ISSUED'), ('ISSUED', 'IN_PROGRESS'), ('IN_PROGRESS', 'RESPONDED'), ('RESPONDED', 'IN_PROGRESS'),
                            ('RESPONDED', 'CLOSED'), ('ISSUED', 'CANCELLED'), ('IN_PROGRESS', 'CANCELLED')) THEN
                            RAISE EXCEPTION 'external_update_request %: % to % is not a step of the request''s state machine', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.status <> 'DRAFT'
                           AND (NEW.contribution_type_item_id, NEW.contribution_schema_code, NEW.target_module, NEW.target_type, NEW.target_id,
                                NEW.instructions, NEW.instructions_lang, NEW.due_date, NEW.issued_at, NEW.issued_by_user_id, NEW.participation_configuration_version_id)
                               IS DISTINCT FROM (OLD.contribution_type_item_id, OLD.contribution_schema_code, OLD.target_module, OLD.target_type, OLD.target_id,
                                OLD.instructions, OLD.instructions_lang, OLD.due_date, OLD.issued_at, OLD.issued_by_user_id, OLD.participation_configuration_version_id) THEN
                            RAISE EXCEPTION 'external_update_request % is %: what it asks, of which source, by when, is what it was issued with', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    END IF;

                    IF NEW.responsible_user_id IS NOT NULL AND NOT external_participation.is_external_user_of(NEW.responsible_user_id, NEW.external_entity_id) THEN
                        RAISE EXCEPTION 'external_update_request %: its responder is an external user of the entity it is addressed to', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.reviewer_user_id IS NOT NULL AND NOT external_participation.is_internal_user(NEW.reviewer_user_id) THEN
                        RAISE EXCEPTION 'external_update_request %: its reviewer is an internal user', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_external_update_request BEFORE INSERT OR UPDATE OR DELETE ON external_participation.external_update_request
                    FOR EACH ROW EXECUTE FUNCTION external_participation.guard_external_update_request();

                CREATE FUNCTION external_participation.guard_external_contribution() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    request external_participation.external_update_request;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'external_contribution % is retained and never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        SELECT * INTO request FROM external_participation.external_update_request r WHERE r.id = NEW.external_update_request_id;
                        IF NEW.status <> 'DRAFT' OR NEW.submitted_at IS NOT NULL OR NEW.review_started_at IS NOT NULL OR NEW.reviewed_at IS NOT NULL
                           OR request.status IN ('DRAFT', 'CLOSED', 'CANCELLED')
                           OR (NEW.project_id, NEW.external_entity_id) IS DISTINCT FROM (request.project_id, request.external_entity_id) THEN
                            RAISE EXCEPTION 'external_contribution %: a revision is born a DRAFT of an issued, open request, with its project and entity', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_external_contribution_born';
                        END IF;

                        IF NEW.previous_revision_id IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM external_participation.external_contribution c
                                           WHERE c.id = NEW.previous_revision_id AND c.external_update_request_id = NEW.external_update_request_id
                                             AND c.status = 'RETURNED' AND c.revision_no = NEW.revision_no - 1) THEN
                            RAISE EXCEPTION 'external_contribution %: a later revision corrects the RETURNED revision before it, of the same request', NEW.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    ELSE
                        IF (NEW.id, NEW.external_update_request_id, NEW.project_id, NEW.external_entity_id, NEW.revision_no, NEW.previous_revision_id, NEW.created_at, NEW.created_by)
                           IS DISTINCT FROM (OLD.id, OLD.external_update_request_id, OLD.project_id, OLD.external_entity_id, OLD.revision_no, OLD.previous_revision_id, OLD.created_at, OLD.created_by) THEN
                            RAISE EXCEPTION 'external_contribution %: its request, anchors, number and predecessor never change', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.status IN ('RETURNED', 'REJECTED', 'APPLIED', 'APPLICATION_FAILED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                            RAISE EXCEPTION 'external_contribution % is % and changes no more', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.status <> OLD.status AND (OLD.status, NEW.status) NOT IN (
                            ('DRAFT', 'SUBMITTED'), ('SUBMITTED', 'UNDER_REVIEW'), ('UNDER_REVIEW', 'RETURNED'), ('UNDER_REVIEW', 'REJECTED'),
                            ('UNDER_REVIEW', 'ACCEPTED_PENDING_APPLICATION'), ('UNDER_REVIEW', 'APPLIED'),
                            ('ACCEPTED_PENDING_APPLICATION', 'APPLIED'), ('ACCEPTED_PENDING_APPLICATION', 'APPLICATION_FAILED')) THEN
                            RAISE EXCEPTION 'external_contribution %: % to % is not a step of the revision''s state machine', OLD.id, OLD.status, NEW.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.status <> 'DRAFT'
                           AND (NEW.contributor_user_id, NEW.submitted_at, NEW.target_version, NEW.target_state)
                               IS DISTINCT FROM (OLD.contributor_user_id, OLD.submitted_at, OLD.target_version, OLD.target_state) THEN
                            RAISE EXCEPTION 'external_contribution % is %: it keeps who submitted it, when, and against which source version', OLD.id, OLD.status
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF NEW.review_started_at IS DISTINCT FROM OLD.review_started_at AND NOT (OLD.status = 'SUBMITTED' AND NEW.status = 'UNDER_REVIEW') THEN
                            RAISE EXCEPTION 'external_contribution %: its review starts once, when it goes under review', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF (NEW.reviewed_by_user_id, NEW.reviewed_at, NEW.review_reason, NEW.review_reason_lang, NEW.review_internal_note, NEW.review_internal_note_lang)
                               IS DISTINCT FROM (OLD.reviewed_by_user_id, OLD.reviewed_at, OLD.review_reason, OLD.review_reason_lang, OLD.review_internal_note, OLD.review_internal_note_lang)
                           AND OLD.status <> 'UNDER_REVIEW' THEN
                            RAISE EXCEPTION 'external_contribution %: it is decided once, by its review', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    END IF;

                    IF NOT external_participation.is_external_user_of(NEW.contributor_user_id, NEW.external_entity_id) THEN
                        RAISE EXCEPTION 'external_contribution %: its contributor is an external user of its entity', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.reviewed_by_user_id IS NOT NULL AND NOT external_participation.is_internal_user(NEW.reviewed_by_user_id) THEN
                        RAISE EXCEPTION 'external_contribution %: it is reviewed by an internal user', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_external_contribution BEFORE INSERT OR UPDATE OR DELETE ON external_participation.external_contribution
                    FOR EACH ROW EXECUTE FUNCTION external_participation.guard_external_contribution();

                -- TASK-066 acceptance criterion 2, for any writer: a value is written only while its revision is a DRAFT of an open request.
                CREATE FUNCTION external_participation.guard_external_contribution_field() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    checked_id uuid := CASE TG_OP WHEN 'DELETE' THEN OLD.external_contribution_id ELSE NEW.external_contribution_id END;
                BEGIN
                    IF TG_OP = 'UPDATE' AND (NEW.id, NEW.external_contribution_id, NEW.field_code, NEW.created_at, NEW.created_by)
                                            IS DISTINCT FROM (OLD.id, OLD.external_contribution_id, OLD.field_code, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'external_contribution_field %: its revision and field never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM external_participation.external_contribution c
                                   JOIN external_participation.external_update_request r ON r.id = c.external_update_request_id
                                   WHERE c.id = checked_id AND c.status = 'DRAFT' AND r.status NOT IN ('CLOSED', 'CANCELLED')) THEN
                        RAISE EXCEPTION 'external_contribution %: its values are written only while it is a DRAFT; a submitted revision is never edited', checked_id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN CASE TG_OP WHEN 'DELETE' THEN OLD ELSE NEW END;
                END;
                $$;

                CREATE TRIGGER guard_external_contribution_field BEFORE INSERT OR UPDATE OR DELETE ON external_participation.external_contribution_field
                    FOR EACH ROW EXECUTE FUNCTION external_participation.guard_external_contribution_field();

                CREATE FUNCTION external_participation.guard_source_application() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'source_application % is retained and never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.revalidated_at IS NOT NULL THEN
                            RAISE EXCEPTION 'source_application %: an attempt is born unrevalidated', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_source_application_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF NOT (OLD.status = 'CONFLICT' AND OLD.revalidated_at IS NULL AND NEW.revalidated_at IS NOT NULL)
                       OR (to_jsonb(NEW) - ARRAY['revalidated_at', 'revalidated_by_user_id', 'revalidated_target_revision_no', 'updated_at', 'updated_by'])
                          IS DISTINCT FROM (to_jsonb(OLD) - ARRAY['revalidated_at', 'revalidated_by_user_id', 'revalidated_target_revision_no', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'source_application %: an attempt never changes, but by the one revalidation of a CONFLICT', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_source_application BEFORE INSERT OR UPDATE OR DELETE ON external_participation.source_application
                    FOR EACH ROW EXECUTE FUNCTION external_participation.guard_source_application();

                -- At commit: a request has at most one revision that is not final, and its status agrees with its revisions.
                CREATE FUNCTION external_participation.check_request_revisions(checked_request_id uuid) RETURNS void
                LANGUAGE plpgsql AS $$
                DECLARE
                    request_status text;
                    open_status text;
                    open_count integer;
                    revision_count integer;
                BEGIN
                    SELECT r.status INTO request_status FROM external_participation.external_update_request r WHERE r.id = checked_request_id;
                    IF request_status IS NULL THEN
                        RETURN;
                    END IF;

                    SELECT count(*) FILTER (WHERE c.status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'ACCEPTED_PENDING_APPLICATION')),
                           max(c.status) FILTER (WHERE c.status IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'ACCEPTED_PENDING_APPLICATION')),
                           count(*)
                    INTO open_count, open_status, revision_count
                    FROM external_participation.external_contribution c WHERE c.external_update_request_id = checked_request_id;

                    IF open_count > 1
                       OR (request_status IN ('DRAFT', 'ISSUED') AND revision_count > 0)
                       OR (request_status = 'IN_PROGRESS' AND open_status IS DISTINCT FROM 'DRAFT')
                       OR (request_status = 'RESPONDED' AND open_status NOT IN ('SUBMITTED', 'UNDER_REVIEW', 'ACCEPTED_PENDING_APPLICATION'))
                       OR (request_status = 'RESPONDED' AND open_status IS NULL)
                       OR (request_status = 'CLOSED' AND open_count > 0)
                       OR (request_status = 'CANCELLED' AND open_status IS DISTINCT FROM 'DRAFT' AND open_count > 0) THEN
                        RAISE EXCEPTION 'external_update_request %: a % request does not have the revisions it has', checked_request_id, request_status
                            USING ERRCODE = 'restrict_violation';
                    END IF;
                END;
                $$;

                CREATE FUNCTION external_participation.check_request_of_request() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM external_participation.check_request_revisions(NEW.id);
                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_request_revisions AFTER INSERT OR UPDATE OF status ON external_participation.external_update_request
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION external_participation.check_request_of_request();

                -- At commit: a revision's request agrees with it, and a revision of a source record is APPLIED only with its APPLIED attempt and
                -- APPLICATION_FAILED only with a FAILED one.
                CREATE FUNCTION external_participation.check_contribution() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    PERFORM external_participation.check_request_revisions(NEW.external_update_request_id);

                    IF NEW.status = 'APPLIED'
                       AND EXISTS (SELECT 1 FROM external_participation.external_update_request r WHERE r.id = NEW.external_update_request_id AND r.target_id IS NOT NULL)
                       AND NOT EXISTS (SELECT 1 FROM external_participation.source_application a WHERE a.external_contribution_id = NEW.id AND a.status = 'APPLIED') THEN
                        RAISE EXCEPTION 'external_contribution %: a revision of a source record is APPLIED by its applied attempt only', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.status = 'APPLICATION_FAILED'
                       AND NOT EXISTS (SELECT 1 FROM external_participation.source_application a WHERE a.external_contribution_id = NEW.id AND a.status = 'FAILED') THEN
                        RAISE EXCEPTION 'external_contribution %: a revision fails its application by a failed attempt only', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_contribution AFTER INSERT OR UPDATE OF status ON external_participation.external_contribution
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION external_participation.check_contribution();

                -- At commit: an attempt is of an accepted revision, and an APPLIED attempt's revision is APPLIED.
                CREATE FUNCTION external_participation.check_source_application() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM external_participation.external_contribution c WHERE c.id = NEW.external_contribution_id
                                   AND c.status IN ('ACCEPTED_PENDING_APPLICATION', 'APPLIED', 'APPLICATION_FAILED')
                                   AND (NEW.status <> 'APPLIED' OR c.status = 'APPLIED')) THEN
                        RAISE EXCEPTION 'source_application %: an attempt is made at an accepted revision, and an applied one leaves it APPLIED', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_source_application AFTER INSERT ON external_participation.source_application
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION external_participation.check_source_application();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER check_source_application ON external_participation.source_application;
                DROP FUNCTION external_participation.check_source_application();
                DROP TRIGGER check_contribution ON external_participation.external_contribution;
                DROP FUNCTION external_participation.check_contribution();
                DROP TRIGGER check_request_revisions ON external_participation.external_update_request;
                DROP FUNCTION external_participation.check_request_of_request();
                DROP FUNCTION external_participation.check_request_revisions(uuid);
                DROP TRIGGER guard_source_application ON external_participation.source_application;
                DROP FUNCTION external_participation.guard_source_application();
                DROP TRIGGER guard_external_contribution_field ON external_participation.external_contribution_field;
                DROP FUNCTION external_participation.guard_external_contribution_field();
                DROP TRIGGER guard_external_contribution ON external_participation.external_contribution;
                DROP FUNCTION external_participation.guard_external_contribution();
                DROP TRIGGER guard_external_update_request ON external_participation.external_update_request;
                DROP FUNCTION external_participation.guard_external_update_request();
                DROP FUNCTION external_participation.is_internal_user(uuid);
                DROP FUNCTION external_participation.is_external_user_of(uuid, uuid);
                DROP TRIGGER refuse_truncation ON external_participation.source_application;
                DROP TRIGGER refuse_truncation ON external_participation.external_contribution_field;
                DROP TRIGGER refuse_truncation ON external_participation.external_contribution;
                DROP TRIGGER refuse_truncation ON external_participation.external_update_request;
                DROP FUNCTION external_participation.refuse_truncation();
                """);
        }
    }
}
