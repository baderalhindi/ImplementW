-- 48-report-rules.sql — the REPORT_RULES version SCR-138's controlled explorer needs (TASK-085; UJ-008). db/seed writes the
-- delivery team's 42-field allowlist as a DRAFT proposal only (reports.md D-12, F-5): until a version is PUBLISHED the explorer
-- answers 422 CONFIGURATION_MISSING. This version is that proposal less the live Approved Budget, exactly as the FG-02
-- integration host publishes it (ReportTestHost), so the allowlist in force has a field it leaves out; the values are test
-- values, not AHDA's decision (docs/testing/e2e-suite.md F-2). Synthetic kind e2e8.
--
-- A PUBLISHED version cannot change (FG-04 guard), so this file runs once per database: re-running it is a no-op.

SELECT NOT EXISTS (SELECT 1 FROM master_data_config.configuration_version WHERE id = '00000000-e2e8-4000-8000-000000000401') AS e2e_report_rules_fresh \gset
\if :e2e_report_rules_fresh

INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                      created_at, created_by, updated_at, updated_by)
SELECT '00000000-e2e8-4000-8000-000000000401', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
       'en', 'E2E data set (TASK-085)', 'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f WHERE f.code = 'REPORT_RULES';

INSERT INTO master_data_config.report_allowlist_entry (id, configuration_version_id, source_entity_code, field_code, label_ar, label_en, is_filterable, is_sortable,
                                                     data_classification_item_id, created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-report-rules:' || e.source_entity_code || '.' || e.field_code)::uuid, '00000000-e2e8-4000-8000-000000000401', e.source_entity_code, e.field_code,
       e.label_ar, e.label_en, e.is_filterable, e.is_sortable, e.data_classification_item_id,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.report_allowlist_entry e
WHERE e.configuration_version_id = md5('configuration_version:REPORT_RULES:1')::uuid
  AND e.source_entity_code || '.' || e.field_code <> 'FINANCIAL_KPI_FINANCIAL_POSITION.APPROVED_BUDGET';

UPDATE master_data_config.configuration_version
SET lifecycle_state = 'PUBLISHED', published_by_user_id = '00000000-0000-4000-8000-0000000000ff', published_at = now() - interval '1 hour',
    effective_from = now() - interval '1 hour'
WHERE id = '00000000-e2e8-4000-8000-000000000401';

\endif
