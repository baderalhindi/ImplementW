-- validate-data-integrity.sql — referential integrity, orphan and constraint check (TASK-027). Record:
-- docs/architecture/seed-data-and-integrity.md
--
-- Run after every migration and every data load. Fails with SQLSTATE 23000 (integrity_constraint_violation), and
-- so exits non-zero, when any check finds a violation; the error message lists every violation, one per line.
-- A clean database ends with one NOTICE saying what was checked.
--
--   ORPHANED_FOREIGN_KEY   a row whose foreign key names no parent row. The database refuses these on insert, but
--                          not when constraint triggers were off during a load (session_replication_role = replica,
--                          pg_restore --disable-triggers) or when the constraint was added NOT VALID.
--   CHECK_VIOLATION        a row that fails a CHECK constraint, including one added NOT VALID.
--   INVALID_INDEX          an index PostgreSQL marks invalid or not ready (a failed CREATE INDEX CONCURRENTLY). An
--                          invalid unique index enforces nothing, so its key may hold duplicates.
--   ORPHANED_AUDIT_ACTOR   created_by or updated_by names no user. ERD D-2 gives those columns no foreign key, so
--                          only this check stands between them and an orphan.
--   WRONG_CATALOGUE        a master data reference pointing at an item of another catalogue, e.g. a project whose
--                          governance profile is a region. A foreign key to master_data_item cannot see this.
--   UNMAPPED_MASTER_DATA_REFERENCE
--                          a *_item_id column this file does not map to a catalogue. Add the column below, and its
--                          catalogue to seed-master-data.sql, in the migration's pull request.
--
-- Every table in every non-system schema is covered, so a table added by a later migration is covered without
-- editing this file; only a new master data reference needs a line in the map. Read-only.
--
-- Pure SQL, no psql meta-command, so the release image runs it unchanged:
--   dotnet PMPlatform.Api.dll validate-data-integrity                                           (each environment)
--   psql "<connection>" -X -q -v ON_ERROR_STOP=1 --single-transaction -f db/seed/validate-data-integrity.sql   (locally)

SET TRANSACTION READ ONLY;

DO $validate$
DECLARE
    violations text[] := '{}';
    foreign_keys integer := 0;
    check_constraints integer := 0;
    indexes integer := 0;
    audited_tables integer := 0;
    item_references integer := 0;
    target record;
    affected bigint;
BEGIN
    -- ORPHANED_FOREIGN_KEY. MATCH SIMPLE: a key with any null column references nothing and is not checked.
    FOR target IN
        SELECT x.conrelid::regclass AS child, x.confrelid::regclass AS parent, x.conname,
               string_agg(format('c.%I IS NOT NULL', ca.attname), ' AND ' ORDER BY k.ord) AS key_present,
               string_agg(format('p.%I = c.%I', pa.attname, ca.attname), ' AND ' ORDER BY k.ord) AS key_matches
        FROM pg_constraint x
        JOIN pg_namespace n ON n.oid = x.connamespace
        CROSS JOIN LATERAL unnest(x.conkey, x.confkey) WITH ORDINALITY AS k (child_attnum, parent_attnum, ord)
        JOIN pg_attribute ca ON ca.attrelid = x.conrelid AND ca.attnum = k.child_attnum
        JOIN pg_attribute pa ON pa.attrelid = x.confrelid AND pa.attnum = k.parent_attnum
        WHERE x.contype = 'f' AND x.conparentid = 0
          AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
        GROUP BY x.oid, x.conrelid, x.confrelid, x.conname
        ORDER BY x.conrelid::regclass::text, x.conname
    LOOP
        EXECUTE format('SELECT count(*) FROM %s AS c WHERE %s AND NOT EXISTS (SELECT 1 FROM %s AS p WHERE %s)',
                       target.child, target.key_present, target.parent, target.key_matches)
            INTO affected;
        foreign_keys := foreign_keys + 1;
        IF affected > 0 THEN
            violations := violations || format('ORPHANED_FOREIGN_KEY %s %s: %s row(s) reference no %s row',
                                               target.child, target.conname, affected, target.parent);
        END IF;
    END LOOP;

    -- CHECK_VIOLATION. A CHECK passes on NULL, and so does NOT (expression) here: only a false result counts.
    FOR target IN
        SELECT x.conrelid::regclass AS relation, x.conname, pg_get_expr(x.conbin, x.conrelid) AS expression
        FROM pg_constraint x
        JOIN pg_namespace n ON n.oid = x.connamespace
        WHERE x.contype = 'c' AND x.conrelid <> 0 AND x.conparentid = 0
          AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
        ORDER BY x.conrelid::regclass::text, x.conname
    LOOP
        EXECUTE format('SELECT count(*) FROM %s WHERE NOT (%s)', target.relation, target.expression) INTO affected;
        check_constraints := check_constraints + 1;
        IF affected > 0 THEN
            violations := violations || format('CHECK_VIOLATION %s %s: %s row(s)', target.relation, target.conname, affected);
        END IF;
    END LOOP;

    -- INVALID_INDEX
    FOR target IN
        SELECT i.indexrelid::regclass AS index_name, i.indrelid::regclass AS relation, i.indisunique, i.indisvalid, i.indisready
        FROM pg_index i
        JOIN pg_class c ON c.oid = i.indexrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
        ORDER BY i.indexrelid::regclass::text
    LOOP
        indexes := indexes + 1;
        IF NOT (target.indisvalid AND target.indisready) THEN
            violations := violations || format('INVALID_INDEX %s on %s: %s%s', target.index_name, target.relation,
                                               CASE WHEN target.indisvalid THEN 'not ready' ELSE 'invalid' END,
                                               CASE WHEN target.indisunique THEN '; its unique key is not enforced' ELSE '' END);
        END IF;
    END LOOP;

    -- ORPHANED_AUDIT_ACTOR, once the user table exists.
    IF to_regclass('identity_access."user"') IS NOT NULL THEN
        FOR target IN
            SELECT c.oid::regclass AS relation
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE c.relkind IN ('r', 'p')
              AND n.nspname NOT IN ('pg_catalog', 'information_schema') AND n.nspname NOT LIKE 'pg\_%'
              AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'created_by' AND NOT a.attisdropped)
              AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'updated_by' AND NOT a.attisdropped)
            ORDER BY c.oid::regclass::text
        LOOP
            EXECUTE format('SELECT count(*) FROM %s AS t WHERE NOT EXISTS (SELECT 1 FROM identity_access."user" u WHERE u.id = t.created_by)'
                           ' OR NOT EXISTS (SELECT 1 FROM identity_access."user" u WHERE u.id = t.updated_by)', target.relation)
                INTO affected;
            audited_tables := audited_tables + 1;
            IF affected > 0 THEN
                violations := violations || format('ORPHANED_AUDIT_ACTOR %s: %s row(s) created or updated by no user', target.relation, affected);
            END IF;
        END LOOP;
    END IF;

    -- WRONG_CATALOGUE and UNMAPPED_MASTER_DATA_REFERENCE, once master data exists. The map covers every master data
    -- reference in erd.dbml; a line whose table does not exist yet is skipped until its module's migration lands.
    IF to_regclass('master_data_config.master_data_item') IS NOT NULL THEN
        -- Every column that references master_data_item by foreign key, except the item's own hierarchy.
        FOR target IN
            WITH item_reference (schema_name, table_name, column_name, catalogue_code) AS (VALUES
            ('identity_access',        'external_entity',                'entity_type_item_id',                  'EXTERNAL_ENTITY_TYPE'),
            ('identity_access',        'permission',                     'data_classification_item_id',          'DATA_CLASSIFICATION'),
            ('master_data_config',     'governance_profile_setting',     'governance_profile_item_id',           'GOVERNANCE_PROFILE'),
            ('master_data_config',     'governance_profile_setting',     'document_control_level_item_id',       'DOCUMENT_CONTROL_LEVEL'),
            ('master_data_config',     'materiality_band',               'governance_profile_item_id',           'GOVERNANCE_PROFILE'),
            ('master_data_config',     'impact_level_definition',        'impact_dimension_item_id',             'IMPACT_DIMENSION'),
            ('master_data_config',     'approval_authority_rule',        'governance_profile_item_id',           'GOVERNANCE_PROFILE'),
            ('master_data_config',     'participation_contribution_rule', 'contribution_type_item_id',           'CONTRIBUTION_TYPE'),
            ('master_data_config',     'evidence_requirement_rule',      'milestone_category_item_id',           'MILESTONE_CATEGORY'),
            ('master_data_config',     'evidence_requirement_rule',      'evidence_type_item_id',                'EVIDENCE_TYPE'),
            ('master_data_config',     'kpi_definition',                 'unit_item_id',                         'KPI_UNIT'),
            ('master_data_config',     'field_classification_rule',      'data_classification_item_id',          'DATA_CLASSIFICATION'),
            ('master_data_config',     'report_allowlist_entry',         'data_classification_item_id',          'DATA_CLASSIFICATION'),
            ('project',                'project',                        'classification_item_id',               'PROJECT_CLASSIFICATION'),
            ('project',                'project',                        'governance_profile_item_id',           'GOVERNANCE_PROFILE'),
            ('project',                'project',                        'region_item_id',                       'REGION'),
            ('project',                'project',                        'city_item_id',                         'CITY'),
            ('schedule',               'project_schedule',               'calendar_item_id',                     'WORKING_CALENDAR'),
            ('schedule',               'project_milestone',              'milestone_category_item_id',           'MILESTONE_CATEGORY'),
            ('project_task',           'project_task',                   'priority_item_id',                     'PRIORITY'),
            ('risk',                   'risk',                           'risk_category_item_id',                'RISK_CATEGORY'),
            ('risk',                   'risk_assessment_impact',         'impact_dimension_item_id',             'IMPACT_DIMENSION'),
            ('management_concern',     'management_concern',             'category_item_id',                     'CONCERN_CATEGORY'),
            ('management_concern',     'management_concern',             'priority_item_id',                     'PRIORITY'),
            ('management_concern',     'management_concern',             'severity_item_id',                     'CONCERN_SEVERITY'),
            ('management_concern',     'concern_impact',                 'impact_dimension_item_id',             'IMPACT_DIMENSION'),
            ('change_request',         'change_request',                 'requested_governance_profile_item_id', 'GOVERNANCE_PROFILE'),
            ('document_management',    'document',                       'document_type_item_id',                'DOCUMENT_TYPE'),
            ('document_management',    'document',                       'data_classification_item_id',          'DATA_CLASSIFICATION'),
            ('document_management',    'evidence_reference',             'evidence_type_item_id',                'EVIDENCE_TYPE'),
            ('external_participation', 'external_update_request',        'request_type_item_id',                 'UPDATE_REQUEST_TYPE'),
            ('external_participation', 'external_contribution',          'contribution_type_item_id',            'CONTRIBUTION_TYPE'),
            ('financial_kpi',          'financial_commitment_line',      'etimad_category_item_id',              'ETIMAD_COST_CATEGORY'),
            ('financial_kpi',          'financial_progress_update_line', 'etimad_category_item_id',              'ETIMAD_COST_CATEGORY'),
            ('financial_kpi',          'kpi_assignment',                 'measurement_frequency_item_id',        'MEASUREMENT_FREQUENCY'),
            ('dashboards',             'dashboard_widget',               'data_classification_item_id',          'DATA_CLASSIFICATION'),
            ('audit_activity',         'audit_event',                    'data_classification_item_id',          'DATA_CLASSIFICATION'))
            SELECT n.nspname AS schema_name, c.relname AS table_name, a.attname AS column_name, r.catalogue_code
            FROM pg_constraint x
            JOIN pg_class c ON c.oid = x.conrelid
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = x.conrelid AND a.attnum = x.conkey[1]
            LEFT JOIN item_reference r ON (r.schema_name, r.table_name, r.column_name) = (n.nspname::text, c.relname::text, a.attname::text)
            WHERE x.contype = 'f' AND x.confrelid = 'master_data_config.master_data_item'::regclass
              AND NOT (x.conrelid = x.confrelid AND a.attname = 'parent_item_id')
            ORDER BY 1, 2, 3
        LOOP
            item_references := item_references + 1;
            IF target.catalogue_code IS NULL THEN
                violations := violations || format('UNMAPPED_MASTER_DATA_REFERENCE %I.%I.%I: no catalogue in validate-data-integrity.sql',
                                                   target.schema_name, target.table_name, target.column_name);
                CONTINUE;
            END IF;
            EXECUTE format('SELECT count(*) FROM %I.%I AS t'
                           ' JOIN master_data_config.master_data_item i ON i.id = t.%I'
                           ' JOIN master_data_config.master_data_catalogue k ON k.id = i.catalogue_id'
                           ' WHERE k.code <> %L', target.schema_name, target.table_name, target.column_name, target.catalogue_code)
                INTO affected;
            IF affected > 0 THEN
                violations := violations || format('WRONG_CATALOGUE %I.%I.%I: %s row(s) reference an item outside %s',
                                                   target.schema_name, target.table_name, target.column_name, affected, target.catalogue_code);
            END IF;
        END LOOP;
    END IF;

    IF cardinality(violations) > 0 THEN
        RAISE EXCEPTION USING
            ERRCODE = 'integrity_constraint_violation',
            MESSAGE = format(E'data integrity: %s violation(s)\n%s', cardinality(violations), array_to_string(violations, E'\n'));
    END IF;

    RAISE NOTICE 'data integrity: no violation in % foreign keys, % check constraints, % indexes, % audited tables, % master data references',
        foreign_keys, check_constraints, indexes, audited_tables, item_references;
END
$validate$;
