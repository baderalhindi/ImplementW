using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMPlatform.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// TASK-034: configuration history is fixed in the database, whoever writes to it. A PUBLISHED configuration version
    /// changes only by its own retirement and a RETIRED one not at all; only a DRAFT is ever deleted; a version's typed
    /// rows change only while it is DRAFT; and a publication's effective-from follows every earlier publication of its
    /// family, checked under a lock on the family row so two concurrent publishers cannot interleave. The migration also
    /// maps <c>xmin</c> as the concurrency token of the four FG-04 rows edited through the API (ERD D-16, R-21), which the
    /// Npgsql SQL generator turns into no DDL in either direction.
    /// </summary>
    public partial class TASK034_GuardConfigurationHistory : Migration
    {
        /// <summary>The typed rows that name their configuration version directly (ERD §5.3).</summary>
        private static readonly string[] ContentTables =
        [
            "configuration_value", "governance_profile_setting", "materiality_band", "impact_level_definition",
            "probability_level_definition", "risk_rating_definition", "risk_matrix_cell", "approval_authority_rule",
            "notification_event_family", "kpi_policy_rule", "participation_contribution_rule", "evidence_requirement_rule",
            "field_classification_rule", "report_allowlist_entry",
        ];

        /// <summary>The typed rows one level down, with the parent that names the version.</summary>
        private static readonly (string Table, string ParentTable, string ParentKey)[] NestedContentTables =
        [
            ("governance_profile_mandatory_field", "governance_profile_setting", "governance_profile_setting_id"),
            ("notification_channel_rule", "notification_event_family", "notification_event_family_id"),
            ("notification_recipient_rule", "notification_event_family", "notification_event_family_id"),
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "master_data_config",
                table: "master_data_item",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "master_data_config",
                table: "master_data_catalogue",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "master_data_config",
                table: "kpi_definition",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "master_data_config",
                table: "configuration_version",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddCheckConstraint(
                name: "ck_configuration_version_effective_from",
                schema: "master_data_config",
                table: "configuration_version",
                sql: "(published_at IS NULL) = (effective_from IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_configuration_version_effective_to",
                schema: "master_data_config",
                table: "configuration_version",
                sql: "effective_to IS NULL OR effective_to >= effective_from");

            migrationBuilder.Sql("""
                CREATE FUNCTION master_data_config.guard_configuration_version() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD.lifecycle_state <> 'DRAFT' THEN
                            RAISE EXCEPTION 'configuration_version % is %: only a DRAFT is ever deleted', OLD.id, OLD.lifecycle_state
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                        RETURN OLD;
                    END IF;

                    IF TG_OP = 'UPDATE' THEN
                        IF OLD.lifecycle_state = 'RETIRED' THEN
                            RAISE EXCEPTION 'configuration_version % is RETIRED and takes no change', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;

                        IF OLD.lifecycle_state = 'PUBLISHED' AND (
                            NEW.lifecycle_state <> 'RETIRED'
                            OR (NEW.id, NEW.configuration_family_id, NEW.version_no, NEW.effective_from, NEW.change_summary,
                                NEW.change_summary_lang, NEW.validated_by_user_id, NEW.validated_at, NEW.published_by_user_id,
                                NEW.published_at, NEW.created_at, NEW.created_by)
                               IS DISTINCT FROM
                               (OLD.id, OLD.configuration_family_id, OLD.version_no, OLD.effective_from, OLD.change_summary,
                                OLD.change_summary_lang, OLD.validated_by_user_id, OLD.validated_at, OLD.published_by_user_id,
                                OLD.published_at, OLD.created_at, OLD.created_by)) THEN
                            RAISE EXCEPTION 'configuration_version % is PUBLISHED: only its retirement may change it', OLD.id
                                USING ERRCODE = 'restrict_violation';
                        END IF;
                    END IF;

                    IF NEW.lifecycle_state = 'PUBLISHED' AND (TG_OP = 'INSERT' OR OLD.lifecycle_state <> 'PUBLISHED') THEN
                        PERFORM 1 FROM master_data_config.configuration_family WHERE id = NEW.configuration_family_id FOR UPDATE;
                        IF EXISTS (
                            SELECT 1 FROM master_data_config.configuration_version v
                            WHERE v.configuration_family_id = NEW.configuration_family_id
                              AND v.id <> NEW.id
                              AND v.published_at IS NOT NULL
                              AND v.effective_from >= NEW.effective_from) THEN
                            RAISE EXCEPTION 'configuration_version %: effective_from must follow every earlier publication of its family', NEW.id
                                USING ERRCODE = 'check_violation', CONSTRAINT = 'ck_configuration_version_effective_from_order';
                        END IF;
                    END IF;

                    RETURN NEW;
                END;
                $$;

                CREATE TRIGGER guard_configuration_version
                    BEFORE INSERT OR UPDATE OR DELETE ON master_data_config.configuration_version
                    FOR EACH ROW EXECUTE FUNCTION master_data_config.guard_configuration_version();

                -- The state of the version a content row belongs to: through configuration_version_id, or, for a row one
                -- level down, through its parent table and key column. NULL when the version is gone, as it is while a
                -- DRAFT's deletion cascades.
                CREATE FUNCTION master_data_config.content_version_state(parent_table text, parent_key text, content_row jsonb) RETURNS text
                LANGUAGE plpgsql STABLE AS $$
                DECLARE
                    version_id uuid;
                BEGIN
                    IF parent_table IS NULL THEN
                        version_id := (content_row ->> 'configuration_version_id')::uuid;
                    ELSE
                        EXECUTE format('SELECT configuration_version_id FROM master_data_config.%I WHERE id = $1', parent_table)
                            INTO version_id
                            USING (content_row ->> parent_key)::uuid;
                    END IF;

                    RETURN (SELECT lifecycle_state FROM master_data_config.configuration_version WHERE id = version_id);
                END;
                $$;

                CREATE FUNCTION master_data_config.guard_configuration_content() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    old_state text;
                    new_state text;
                BEGIN
                    IF TG_OP IN ('UPDATE', 'DELETE') THEN
                        old_state := master_data_config.content_version_state(TG_ARGV[0], TG_ARGV[1], to_jsonb(OLD));
                    END IF;

                    IF TG_OP IN ('INSERT', 'UPDATE') THEN
                        new_state := master_data_config.content_version_state(TG_ARGV[0], TG_ARGV[1], to_jsonb(NEW));
                    END IF;

                    IF old_state <> 'DRAFT' OR new_state <> 'DRAFT' THEN
                        RAISE EXCEPTION '%: the configuration version is % and its content is fixed', TG_TABLE_NAME, coalesce(old_state, new_state)
                            USING ERRCODE = 'restrict_violation';
                    END IF;

                    RETURN CASE TG_OP WHEN 'DELETE' THEN OLD ELSE NEW END;
                END;
                $$;
                """);

            foreach (string table in ContentTables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER guard_configuration_content
                        BEFORE INSERT OR UPDATE OR DELETE ON master_data_config.{table}
                        FOR EACH ROW EXECUTE FUNCTION master_data_config.guard_configuration_content();
                    """);
            }

            foreach ((string table, string parentTable, string parentKey) in NestedContentTables)
            {
                migrationBuilder.Sql($"""
                    CREATE TRIGGER guard_configuration_content
                        BEFORE INSERT OR UPDATE OR DELETE ON master_data_config.{table}
                        FOR EACH ROW EXECUTE FUNCTION master_data_config.guard_configuration_content('{parentTable}', '{parentKey}');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (string table in ContentTables.Concat(NestedContentTables.Select(t => t.Table)))
            {
                migrationBuilder.Sql($"DROP TRIGGER guard_configuration_content ON master_data_config.{table};");
            }

            migrationBuilder.Sql("""
                DROP FUNCTION master_data_config.guard_configuration_content();
                DROP FUNCTION master_data_config.content_version_state(text, text, jsonb);
                DROP TRIGGER guard_configuration_version ON master_data_config.configuration_version;
                DROP FUNCTION master_data_config.guard_configuration_version();
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_configuration_version_effective_from",
                schema: "master_data_config",
                table: "configuration_version");

            migrationBuilder.DropCheckConstraint(
                name: "ck_configuration_version_effective_to",
                schema: "master_data_config",
                table: "configuration_version");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "master_data_config",
                table: "master_data_item");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "master_data_config",
                table: "master_data_catalogue");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "master_data_config",
                table: "kpi_definition");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "master_data_config",
                table: "configuration_version");
        }
    }
}
