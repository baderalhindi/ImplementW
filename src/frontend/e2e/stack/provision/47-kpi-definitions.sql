-- 47-kpi-definitions.sql — the KPI definitions WF-14's KPI Performance needs before a KPI can be assigned to a project
-- (TASK-085; UJ-009, UJ-010). db/seed publishes none (financial-kpi.md D-14, F-5: AHDA's KPI library arrives through FG-04),
-- so `POST /kpi-assignments` has nothing to name. One HIGHER_IS_BETTER KPI in the data set's percent unit, as the WF-14
-- integration host defines TEST_SAFE_HOURS (FinancialKpiTestHost); a test definition, not AHDA's library
-- (docs/testing/e2e-suite.md F-2). The governed lifecycle needs three people with CONFIGURATION_MANAGE and the data set has
-- one, so the definition is written PUBLISHED, as db/seed writes its master data. Synthetic kind e2e9 (master data).
--
-- Idempotent: safe to re-run.

INSERT INTO master_data_config.kpi_definition (id, code, name_ar, name_en, description_ar, description_en, unit_item_id, direction, lifecycle_state,
                                               published_at, published_by_user_id, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e9-4000-8000-000000000401', 'E2E_SAFE_HOURS', 'ساعات العمل الآمنة', 'Safe working hours',
        'نسبة ساعات العمل دون حوادث.', 'Share of working hours without an incident.', '00000000-e2e9-4000-8000-0000000000c1', 'HIGHER_IS_BETTER',
        'PUBLISHED', now() - interval '1 hour', '00000000-0000-4000-8000-0000000000ff',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO NOTHING;
