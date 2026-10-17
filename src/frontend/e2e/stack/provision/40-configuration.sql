-- 40-configuration.sql — the published configuration every journey shares (TASK-085): GOVERNANCE_PROFILE, WORKFLOW_POLICY
-- and APPROVAL_AUTHORITY. Nothing is published in any family by db/seed; AHDA's values arrive through FG-04. Resolution takes
-- the newest PUBLISHED version of a family, so each of these three families has exactly one version here, and a journey's
-- own family (risk matrix, notification routing, report rules …) lives in its own 4n-*.sql file.
--
-- A PUBLISHED version cannot change (FG-04 guard), so this file runs once per database: re-running it is a no-op. To change
-- a value, add a version 2 in a new file rather than editing this one on a provisioned database.
--
-- Every route is decided by R02 (the e2e Portfolio Manager); a band-3 change request also needs R03, as the integration
-- hosts configure it. The values are the integration suites' test values, not AHDA policy (docs/testing/e2e-suite.md F-2).
-- Synthetic kind e2e8.

SELECT NOT EXISTS (SELECT 1 FROM master_data_config.configuration_version WHERE id = '00000000-e2e8-4000-8000-000000000101') AS e2e_configuration_fresh \gset
\if :e2e_configuration_fresh

INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                      created_at, created_by, updated_at, updated_by)
SELECT v.id::uuid, f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
       'en', 'E2E data set (TASK-085)', 'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('00000000-e2e8-4000-8000-000000000101', 'GOVERNANCE_PROFILE'),
             ('00000000-e2e8-4000-8000-000000000102', 'WORKFLOW_POLICY'),
             ('00000000-e2e8-4000-8000-000000000103', 'APPROVAL_AUTHORITY'),
             ('00000000-e2e8-4000-8000-000000000104', 'MATERIALITY_BAND')) AS v (id, family)
JOIN master_data_config.configuration_family f ON f.code = v.family;

-- A 7-day update cadence, baseline approval and risk management for STANDARD and FULL, three change bands.
INSERT INTO master_data_config.governance_profile_setting (id, configuration_version_id, governance_profile_item_id, requires_baseline_approval, risk_management_required,
                                                           change_band_count, update_cadence_days, document_control_level_item_id, included_in_reporting,
                                                           created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-governance:' || i.code)::uuid, '00000000-e2e8-4000-8000-000000000101', i.id, i.code <> 'LIGHT', i.code <> 'LIGHT', 3, 7,
       '00000000-e2e9-4000-8000-000000000031', true, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
WHERE c.code = 'GOVERNANCE_PROFILE';

INSERT INTO master_data_config.configuration_value (id, configuration_version_id, value_key, value_text, value_type, created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-workflow-policy:' || v.value_key)::uuid, '00000000-e2e8-4000-8000-000000000102', v.value_key, v.value_text, v.value_type,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('APPROVAL_TASK_DUE_DAYS', '3', 'DURATION_DAYS'),
             ('APPROVAL_ESCALATION_ROLE', 'R02', 'TEXT'),
             ('CONCERN_ESCALATION_ROLE', 'R03', 'TEXT'),
             ('PROGRESS_HEALTH_AMBER_SLIPPAGE_PERCENT', '5', 'PERCENT'),
             ('PROGRESS_HEALTH_RED_SLIPPAGE_PERCENT', '10', 'PERCENT'),
             ('SCHEDULE_HEALTH_AMBER_FINISH_VARIANCE_DAYS', '5', 'DURATION_DAYS'),
             ('SCHEDULE_HEALTH_RED_FINISH_VARIANCE_DAYS', '20', 'DURATION_DAYS'),
             ('FINANCIAL_STATUS_AMBER_OVERRUN_PERCENT', '5', 'PERCENT'),
             ('FINANCIAL_STATUS_RED_OVERRUN_PERCENT', '10', 'PERCENT')) AS v (value_key, value_text, value_type);

INSERT INTO master_data_config.approval_authority_rule (id, configuration_version_id, subject_type_code, band_no, sequence_no, approver_role_id, is_mandatory,
                                                        created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-approval:' || r.subject || ':' || coalesce(r.band::text, '-') || ':' || r.seq)::uuid, '00000000-e2e8-4000-8000-000000000103',
       r.subject, r.band, r.seq, ('00000000-0000-4000-8000-' || lpad(r.role::text, 12, '0'))::uuid, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('PROJECT_REGISTRATION', NULL::integer, 1, 2),
             ('SCHEDULE_BASELINE', NULL, 1, 2),
             ('MILESTONE_ACHIEVEMENT', NULL, 1, 2),
             ('CHANGE_REQUEST', NULL, 1, 2),
             ('CHANGE_REQUEST', 3, 2, 3),
             ('COMMITMENT', NULL, 1, 2),
             ('KPI_TARGET', NULL, 1, 2),
             ('MANAGEMENT_CONCERN_RESOLUTION', NULL, 1, 2),
             ('SUSPENSION', NULL, 1, 2),
             ('RESUMPTION', NULL, 1, 2),
             ('COMPLETION', NULL, 1, 2),
             ('CLOSURE', NULL, 1, 2)) AS r (subject, band, seq, role);

-- Change bands as the change-request host configures them: band 2 at 5% of budget or 10 days, band 3 at 200,000 SAR, half the
-- baseline's duration or any scope change.
INSERT INTO master_data_config.materiality_band (id, configuration_version_id, governance_profile_item_id, band_no, cost_threshold_pct, cost_threshold_sar,
                                                 schedule_threshold_pct, schedule_threshold_days, scope_rule_code, requires_approval,
                                                 created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-band:' || i.code || ':' || b.band_no)::uuid, '00000000-e2e8-4000-8000-000000000104', i.id, b.band_no, b.cost_pct, b.cost_sar,
       b.schedule_pct, b.schedule_days, b.scope_rule, b.band_no > 1, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES (1, NULL::numeric, NULL::numeric, NULL::numeric, NULL::integer, NULL::text),
             (2, 5, NULL, NULL, 10, NULL),
             (3, NULL, 200000, 50, NULL, 'ANY_SCOPE_CHANGE')) AS b (band_no, cost_pct, cost_sar, schedule_pct, schedule_days, scope_rule)
CROSS JOIN master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
WHERE c.code = 'GOVERNANCE_PROFILE';

UPDATE master_data_config.configuration_version
SET lifecycle_state = 'PUBLISHED', published_by_user_id = '00000000-0000-4000-8000-0000000000ff', published_at = now() - interval '1 hour',
    effective_from = now() - interval '1 hour'
WHERE id IN ('00000000-e2e8-4000-8000-000000000101', '00000000-e2e8-4000-8000-000000000102', '00000000-e2e8-4000-8000-000000000103',
             '00000000-e2e8-4000-8000-000000000104');

\endif
