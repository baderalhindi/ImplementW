using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-037, 3 of 3: document history is fixed in the database, whoever writes to it (CTL-20). No row of the four tables
    /// is ever deleted (RETAIN), so unlinking can never remove a file or a version. A version is born SCAN_PENDING; its file,
    /// name, size, checksum and document never change; only the scanner's verdict is written, CLEAN and QUARANTINED are
    /// final, and SCAN_FAILED may only go back to SCAN_PENDING. A document's project and owner never change and an ARCHIVED
    /// document changes no more. A link ends once and stays ended. Evidence is pinned only to a CLEAN version of the link's
    /// own document on a link not ended, and changes only by being withdrawn.
    /// </summary>
    public partial class TASK037_GuardDocumentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION document_management.refuse_deletion() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'document_management.% % is document history and is never deleted', TG_TABLE_NAME, OLD.id
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE FUNCTION document_management.guard_document() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.project_id, NEW.owner_user_id, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM (OLD.id, OLD.project_id, OLD.owner_user_id, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'document %: its project and owner never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.status = 'ARCHIVED' AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'document % is ARCHIVED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_document
                    BEFORE UPDATE ON document_management.document
                    FOR EACH ROW EXECUTE FUNCTION document_management.guard_document();

                CREATE FUNCTION document_management.guard_document_version() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'INSERT' THEN
                        IF NEW.scan_state <> 'SCAN_PENDING' THEN
                            RAISE EXCEPTION 'document_version %: a new version is SCAN_PENDING until the scanner decides', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_document_version_born_pending';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (to_jsonb(NEW) - ARRAY['scan_state', 'scan_completed_at', 'scan_reference', 'updated_at', 'updated_by'])
                       IS DISTINCT FROM
                       (to_jsonb(OLD) - ARRAY['scan_state', 'scan_completed_at', 'scan_reference', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'document_version %: a version''s file and identity never change; a new file is a new version', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.scan_state IN ('CLEAN', 'QUARANTINED') AND to_jsonb(NEW) IS DISTINCT FROM to_jsonb(OLD) THEN
                        RAISE EXCEPTION 'document_version % is %: the verdict is final', OLD.id, OLD.scan_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.scan_state = 'SCAN_FAILED' AND NEW.scan_state NOT IN ('SCAN_FAILED', 'SCAN_PENDING') THEN
                        RAISE EXCEPTION 'document_version % is SCAN_FAILED: it is scanned again from SCAN_PENDING', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.scan_state = 'SCAN_FAILED' AND NEW.scan_state = 'SCAN_FAILED'
                       AND (NEW.scan_completed_at, NEW.scan_reference) IS DISTINCT FROM (OLD.scan_completed_at, OLD.scan_reference) THEN
                        RAISE EXCEPTION 'document_version %: a verdict is not rewritten', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_document_version
                    BEFORE INSERT OR UPDATE ON document_management.document_version
                    FOR EACH ROW EXECUTE FUNCTION document_management.guard_document_version();

                CREATE FUNCTION document_management.guard_business_link() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF OLD.unlinked_at IS NOT NULL
                       OR (to_jsonb(NEW) - ARRAY['unlinked_at', 'unlinked_by_user_id', 'updated_at', 'updated_by'])
                          IS DISTINCT FROM
                          (to_jsonb(OLD) - ARRAY['unlinked_at', 'unlinked_by_user_id', 'updated_at', 'updated_by']) THEN
                        RAISE EXCEPTION 'business_link %: a link changes only by being unlinked, once', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_business_link
                    BEFORE UPDATE ON document_management.business_link
                    FOR EACH ROW EXECUTE FUNCTION document_management.guard_business_link();

                CREATE FUNCTION document_management.guard_evidence_reference() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    version_state text;
                    version_document uuid;
                    link_document uuid;
                    link_ended timestamptz;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF OLD.status <> 'VALID'
                           OR (to_jsonb(NEW) - ARRAY['status', 'updated_at', 'updated_by'])
                              IS DISTINCT FROM
                              (to_jsonb(OLD) - ARRAY['status', 'updated_at', 'updated_by']) THEN
                            RAISE EXCEPTION 'evidence_reference %: evidence changes only by being withdrawn, once', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        RETURN NEW;
                    END IF;

                    SELECT v.scan_state, v.document_id INTO version_state, version_document
                    FROM document_management.document_version v WHERE v.id = NEW.document_version_id;
                    SELECT l.document_id, l.unlinked_at INTO link_document, link_ended
                    FROM document_management.business_link l WHERE l.id = NEW.business_link_id;

                    IF version_state IS DISTINCT FROM 'CLEAN' THEN
                        RAISE EXCEPTION 'evidence_reference %: version % is %, and only a CLEAN version is evidence', NEW.id, NEW.document_version_id, version_state
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_evidence_reference_clean_version';
                    END IF;

                    IF version_document IS DISTINCT FROM link_document OR link_ended IS NOT NULL OR NEW.status <> 'VALID' THEN
                        RAISE EXCEPTION 'evidence_reference %: evidence is VALID when pinned, on a link not ended, to a version of the link''s own document', NEW.id
                            USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_evidence_reference_pin';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_evidence_reference
                    BEFORE INSERT OR UPDATE ON document_management.evidence_reference
                    FOR EACH ROW EXECUTE FUNCTION document_management.guard_evidence_reference();

                CREATE TRIGGER refuse_deletion BEFORE DELETE ON document_management.document FOR EACH ROW EXECUTE FUNCTION document_management.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON document_management.document_version FOR EACH ROW EXECUTE FUNCTION document_management.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON document_management.business_link FOR EACH ROW EXECUTE FUNCTION document_management.refuse_deletion();
                CREATE TRIGGER refuse_deletion BEFORE DELETE ON document_management.evidence_reference FOR EACH ROW EXECUTE FUNCTION document_management.refuse_deletion();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER refuse_deletion ON document_management.evidence_reference;
                DROP TRIGGER refuse_deletion ON document_management.business_link;
                DROP TRIGGER refuse_deletion ON document_management.document_version;
                DROP TRIGGER refuse_deletion ON document_management.document;
                DROP TRIGGER guard_evidence_reference ON document_management.evidence_reference;
                DROP FUNCTION document_management.guard_evidence_reference();
                DROP TRIGGER guard_business_link ON document_management.business_link;
                DROP FUNCTION document_management.guard_business_link();
                DROP TRIGGER guard_document_version ON document_management.document_version;
                DROP FUNCTION document_management.guard_document_version();
                DROP TRIGGER guard_document ON document_management.document;
                DROP FUNCTION document_management.guard_document();
                DROP FUNCTION document_management.refuse_deletion();
                """);
        }
    }
}
