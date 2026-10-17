-- 45-participation.sql — the PARTICIPATION version WF-13 needs before any update request is issued (TASK-085; UJ-005 and the
-- ADR-013 participation specs). Nothing is published in this family by db/seed (external-participation.md F-3): issue fails
-- closed, 422 CONFIGURATION_MISSING. This version enables the two typed contribution types of 30-master-data.sql in both
-- participation modes, as the WF-13 integration host does for TASK_PROGRESS (ExternalParticipationTestHost); the values are
-- test values, not AHDA policy (docs/testing/e2e-suite.md F-2). Synthetic kind e2e8.
--
-- A PUBLISHED version cannot change (FG-04 guard), so this file runs once per database: re-running it is a no-op.

SELECT NOT EXISTS (SELECT 1 FROM master_data_config.configuration_version WHERE id = '00000000-e2e8-4000-8000-000000000301') AS e2e_participation_fresh \gset
\if :e2e_participation_fresh

INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                      created_at, created_by, updated_at, updated_by)
SELECT '00000000-e2e8-4000-8000-000000000301', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
       'en', 'E2E data set (TASK-085)', 'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f WHERE f.code = 'PARTICIPATION';

INSERT INTO master_data_config.participation_contribution_rule (id, configuration_version_id, participation_mode, contribution_type_item_id, is_enabled,
                                                                created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-participation:' || r.mode || ':' || r.type_id)::uuid, '00000000-e2e8-4000-8000-000000000301', r.mode, r.type_id::uuid, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('ENTITY_MANAGED', '00000000-e2e9-4000-8000-000000000301'), ('AHDA_MANAGED', '00000000-e2e9-4000-8000-000000000301'),
             ('ENTITY_MANAGED', '00000000-e2e9-4000-8000-000000000302'), ('AHDA_MANAGED', '00000000-e2e9-4000-8000-000000000302')) AS r (mode, type_id);

UPDATE master_data_config.configuration_version
SET lifecycle_state = 'PUBLISHED', published_by_user_id = '00000000-0000-4000-8000-0000000000ff', published_at = now() - interval '1 hour',
    effective_from = now() - interval '1 hour'
WHERE id = '00000000-e2e8-4000-8000-000000000301';

\endif
