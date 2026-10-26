using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-069, 3 of 3: FG-01's governance held in the database, whoever writes (FG-01 §13.1, BR-DSH-031).
    /// <list type="bullet">
    /// <item>A definition is retained, never deleted or truncated. It is born a DRAFT — the platform seed alone ships PUBLISHED versions,
    /// as it ships master data — keeps its code, number and author, and moves only DRAFT → VALIDATED → PUBLISHED and any state but RETIRED →
    /// RETIRED. Its content changes only while DRAFT; a PUBLISHED version changes only by being retired, and a RETIRED one not at all.</item>
    /// <item>A version's widgets and audience are written only while it is a DRAFT (the seed's own inserts aside), so a PUBLISHED dashboard is
    /// immutable whatever writes to it.</item>
    /// <item>A personal preference is of a PUBLISHED version, and names only that version's optional widgets (ADR-019, DSH-CC-29).</item>
    /// </list>
    /// At commit (deferred, so publishing a version and retiring the one it replaces may be written in either order): a code has at most one
    /// PUBLISHED version, and a role is the default landing of at most one PUBLISHED version (Blueprint §20.2).
    /// </summary>
    public partial class TASK069_GuardDashboardDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION dashboards.refuse_truncation() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'dashboards.% is retained and never truncated', TG_TABLE_NAME
                        USING ERRCODE = 'restrict_violation';
                END;
                $$;

                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON dashboards.dashboard_definition FOR EACH STATEMENT EXECUTE FUNCTION dashboards.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON dashboards.dashboard_widget FOR EACH STATEMENT EXECUTE FUNCTION dashboards.refuse_truncation();
                CREATE TRIGGER refuse_truncation BEFORE TRUNCATE ON dashboards.dashboard_audience_role FOR EACH STATEMENT EXECUTE FUNCTION dashboards.refuse_truncation();

                -- The platform seed principal (seed-master-data.sql §1): the one writer that ships PUBLISHED versions.
                CREATE FUNCTION dashboards.is_seed(writer uuid) RETURNS boolean
                LANGUAGE sql IMMUTABLE AS $$ SELECT writer = '00000000-0000-4000-8000-0000000000ff'::uuid $$;

                CREATE FUNCTION dashboards.guard_dashboard_definition() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'dashboard_definition % is retained: a version no longer used is retired, never deleted', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF TG_OP = 'INSERT' THEN
                        IF NEW.lifecycle_state <> 'DRAFT' AND NOT dashboards.is_seed(NEW.created_by) THEN
                            RAISE EXCEPTION 'dashboard_definition %: a version is born a DRAFT', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_dashboard_definition_born';
                        END IF;

                        RETURN NEW;
                    END IF;

                    IF (NEW.id, NEW.code, NEW.version_no, NEW.created_at, NEW.created_by) IS DISTINCT FROM (OLD.id, OLD.code, OLD.version_no, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'dashboard_definition %: its code, number and author never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state = 'RETIRED' THEN
                        RAISE EXCEPTION 'dashboard_definition % is RETIRED and changes no more', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NEW.lifecycle_state <> OLD.lifecycle_state AND (OLD.lifecycle_state, NEW.lifecycle_state) NOT IN (
                        ('DRAFT', 'VALIDATED'), ('VALIDATED', 'PUBLISHED'), ('DRAFT', 'RETIRED'), ('VALIDATED', 'RETIRED'), ('PUBLISHED', 'RETIRED')) THEN
                        RAISE EXCEPTION 'dashboard_definition %: % to % is not a step of the governed lifecycle', OLD.id, OLD.lifecycle_state, NEW.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state <> 'DRAFT'
                       AND (NEW.name_ar, NEW.name_en, NEW.description_ar, NEW.description_en, NEW.allows_personalization)
                           IS DISTINCT FROM (OLD.name_ar, OLD.name_en, OLD.description_ar, OLD.description_en, OLD.allows_personalization) THEN
                        RAISE EXCEPTION 'dashboard_definition % is %: its content changed only while DRAFT; a change is a new version', OLD.id, OLD.lifecycle_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF OLD.lifecycle_state = 'PUBLISHED'
                       AND (NEW.validated_by_user_id, NEW.validated_at, NEW.published_by_user_id, NEW.published_at)
                           IS DISTINCT FROM (OLD.validated_by_user_id, OLD.validated_at, OLD.published_by_user_id, OLD.published_at) THEN
                        RAISE EXCEPTION 'dashboard_definition % is PUBLISHED: only its retirement may change it', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_dashboard_definition BEFORE INSERT OR UPDATE OR DELETE ON dashboards.dashboard_definition
                    FOR EACH ROW EXECUTE FUNCTION dashboards.guard_dashboard_definition();

                -- A version's widgets and audience are its content: written only while it is a DRAFT.
                CREATE FUNCTION dashboards.guard_definition_content() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    row_definition_id uuid := CASE WHEN TG_OP = 'DELETE' THEN OLD.dashboard_definition_id ELSE NEW.dashboard_definition_id END;
                    definition_state text;
                BEGIN
                    IF TG_OP = 'UPDATE' AND NEW.dashboard_definition_id <> OLD.dashboard_definition_id THEN
                        RAISE EXCEPTION 'dashboards.% %: a row never moves to another version', TG_TABLE_NAME, OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    SELECT d.lifecycle_state INTO definition_state FROM dashboards.dashboard_definition d WHERE d.id = row_definition_id;
                    IF definition_state <> 'DRAFT' AND NOT (TG_OP = 'INSERT' AND dashboards.is_seed(NEW.created_by)) THEN
                        RAISE EXCEPTION 'dashboards.%: version % is %, and only a DRAFT''s content is written', TG_TABLE_NAME, row_definition_id, definition_state
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
                END;
                $$;

                CREATE TRIGGER guard_dashboard_widget BEFORE INSERT OR UPDATE OR DELETE ON dashboards.dashboard_widget
                    FOR EACH ROW EXECUTE FUNCTION dashboards.guard_definition_content();
                CREATE TRIGGER guard_dashboard_audience_role BEFORE INSERT OR UPDATE OR DELETE ON dashboards.dashboard_audience_role
                    FOR EACH ROW EXECUTE FUNCTION dashboards.guard_definition_content();

                -- ADR-019: a preference is of a PUBLISHED version; its owner and version never change.
                CREATE FUNCTION dashboards.guard_user_dashboard_preference() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' AND (NEW.user_id, NEW.dashboard_definition_id) IS DISTINCT FROM (OLD.user_id, OLD.dashboard_definition_id) THEN
                        RAISE EXCEPTION 'user_dashboard_preference %: its owner and version never change', OLD.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM dashboards.dashboard_definition d
                                   WHERE d.id = NEW.dashboard_definition_id AND d.lifecycle_state = 'PUBLISHED' AND d.allows_personalization) THEN
                        RAISE EXCEPTION 'user_dashboard_preference %: a preference is of a PUBLISHED version that allows personalisation', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_user_dashboard_preference BEFORE INSERT OR UPDATE ON dashboards.user_dashboard_preference
                    FOR EACH ROW EXECUTE FUNCTION dashboards.guard_user_dashboard_preference();

                -- DSH-CC-29: a preference names only its own version's optional widgets.
                CREATE FUNCTION dashboards.guard_user_dashboard_widget_preference() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM dashboards.user_dashboard_preference p
                                   JOIN dashboards.dashboard_widget w ON w.dashboard_definition_id = p.dashboard_definition_id
                                   WHERE p.id = NEW.user_dashboard_preference_id AND w.id = NEW.dashboard_widget_id AND w.is_optional_visibility) THEN
                        RAISE EXCEPTION 'user_dashboard_widget_preference %: a preference names an optional widget of its own version', NEW.id
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_user_dashboard_widget_preference BEFORE INSERT OR UPDATE ON dashboards.user_dashboard_widget_preference
                    FOR EACH ROW EXECUTE FUNCTION dashboards.guard_user_dashboard_widget_preference();

                -- At commit: one PUBLISHED version per code (ADR-006: three dashboards, each in force once), and one default landing per role.
                CREATE FUNCTION dashboards.check_published_definitions() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (SELECT count(*) FROM dashboards.dashboard_definition d WHERE d.code = NEW.code AND d.lifecycle_state = 'PUBLISHED') > 1 THEN
                        RAISE EXCEPTION 'dashboard %: one version is PUBLISHED at a time', NEW.code
                            USING ERRCODE = 'unique_violation', CONSTRAINT = 'ck_dashboard_definition_one_published';
                    END IF;

                    IF EXISTS (SELECT 1 FROM dashboards.dashboard_audience_role a
                               JOIN dashboards.dashboard_definition d ON d.id = a.dashboard_definition_id
                               WHERE d.lifecycle_state = 'PUBLISHED' AND a.is_default_landing
                               GROUP BY a.role_id HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'dashboard %: a role lands by default on one PUBLISHED dashboard', NEW.code
                            USING ERRCODE = 'unique_violation', CONSTRAINT = 'ck_dashboard_audience_role_one_default_landing';
                    END IF;

                    RETURN NULL;
                END;
                $$;

                CREATE CONSTRAINT TRIGGER check_published_definitions AFTER INSERT OR UPDATE OF lifecycle_state ON dashboards.dashboard_definition
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION dashboards.check_published_definitions();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER check_published_definitions ON dashboards.dashboard_definition;
                DROP FUNCTION dashboards.check_published_definitions();
                DROP TRIGGER guard_user_dashboard_widget_preference ON dashboards.user_dashboard_widget_preference;
                DROP FUNCTION dashboards.guard_user_dashboard_widget_preference();
                DROP TRIGGER guard_user_dashboard_preference ON dashboards.user_dashboard_preference;
                DROP FUNCTION dashboards.guard_user_dashboard_preference();
                DROP TRIGGER guard_dashboard_audience_role ON dashboards.dashboard_audience_role;
                DROP TRIGGER guard_dashboard_widget ON dashboards.dashboard_widget;
                DROP FUNCTION dashboards.guard_definition_content();
                DROP TRIGGER guard_dashboard_definition ON dashboards.dashboard_definition;
                DROP FUNCTION dashboards.guard_dashboard_definition();
                DROP FUNCTION dashboards.is_seed(uuid);
                DROP TRIGGER refuse_truncation ON dashboards.dashboard_audience_role;
                DROP TRIGGER refuse_truncation ON dashboards.dashboard_widget;
                DROP TRIGGER refuse_truncation ON dashboards.dashboard_definition;
                DROP FUNCTION dashboards.refuse_truncation();
                """);
        }
    }
}
