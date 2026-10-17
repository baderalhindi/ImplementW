-- 50-risk-matrix.sql — the first published RISK_MATRIX version (TASK-085). db/seed ships RISK_MATRIX version 1 as a DRAFT
-- with the generic five-level scale and no ratings or mapping (OQ-006), so until a version is published no risk can be
-- assessed and no matrix drawn (risk-management-ui.md D-3). This version copies the seed's probability and impact levels and
-- adds three ratings, LOW < MEDIUM < HIGH, mapped by probability × impact: HIGH from 15, MEDIUM from 6, LOW below.
--
-- UJ-012 (published configuration) publishes later versions through FG-04 on every run. Each keeps these levels, these
-- dimensions and these three rating codes and only remaps cells, so a journey that assesses a risk can rely on the matrix
-- being valid and 5 × 5 with LOW/MEDIUM/HIGH, but never on the rating of a particular cell.
--
-- A PUBLISHED version cannot change (FG-04 guard): this file runs once per database. The values are test values, not AHDA
-- policy (docs/testing/e2e-suite.md F-2). Synthetic kind e2e8.

SELECT NOT EXISTS (SELECT 1 FROM master_data_config.configuration_version WHERE id = '00000000-e2e8-4000-8000-000000000601') AS e2e_risk_matrix_fresh \gset
\if :e2e_risk_matrix_fresh

INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                      created_at, created_by, updated_at, updated_by)
SELECT '00000000-e2e8-4000-8000-000000000601', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
       'en', 'E2E data set (TASK-085): the seed scale with LOW, MEDIUM and HIGH', 'DRAFT',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f WHERE f.code = 'RISK_MATRIX';

-- The seed's DRAFT version 1 (db/seed: md5('configuration_version:RISK_MATRIX:1')) holds the scale.
INSERT INTO master_data_config.probability_level_definition (id, configuration_version_id, level, lower_pct, upper_pct, label_ar, label_en,
                                                             created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-risk-probability:' || s.level)::uuid, '00000000-e2e8-4000-8000-000000000601', s.level, s.lower_pct, s.upper_pct, s.label_ar, s.label_en,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.probability_level_definition s
WHERE s.configuration_version_id = md5('configuration_version:RISK_MATRIX:1')::uuid;

INSERT INTO master_data_config.impact_level_definition (id, configuration_version_id, impact_dimension_item_id, level, lower_bound, upper_bound,
                                                        description_ar, description_en, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-risk-impact:' || s.impact_dimension_item_id || ':' || s.level)::uuid, '00000000-e2e8-4000-8000-000000000601', s.impact_dimension_item_id,
       s.level, s.lower_bound, s.upper_bound, s.description_ar, s.description_en, s.label_ar, s.label_en,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.impact_level_definition s
WHERE s.configuration_version_id = md5('configuration_version:RISK_MATRIX:1')::uuid;

INSERT INTO master_data_config.risk_rating_definition (id, configuration_version_id, code, sort_order, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-risk-rating:' || r.code)::uuid, '00000000-e2e8-4000-8000-000000000601', r.code, r.sort_order, r.label_ar, r.label_en,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('LOW', 1, 'منخفض', 'Low'), ('MEDIUM', 2, 'متوسط', 'Medium'), ('HIGH', 3, 'مرتفع', 'High')) AS r (code, sort_order, label_ar, label_en);

INSERT INTO master_data_config.risk_matrix_cell (id, configuration_version_id, probability_level, impact_level, risk_rating_definition_id,
                                                 created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-risk-cell:' || p || ':' || i)::uuid, '00000000-e2e8-4000-8000-000000000601', p, i,
       md5('e2e-risk-rating:' || CASE WHEN p * i >= 15 THEN 'HIGH' WHEN p * i >= 6 THEN 'MEDIUM' ELSE 'LOW' END)::uuid,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM generate_series(1, 5) AS p CROSS JOIN generate_series(1, 5) AS i;

UPDATE master_data_config.configuration_version
SET lifecycle_state = 'PUBLISHED', published_by_user_id = '00000000-0000-4000-8000-0000000000ff', published_at = now() - interval '1 hour',
    effective_from = now() - interval '1 hour'
WHERE id = '00000000-e2e8-4000-8000-000000000601';

\endif
