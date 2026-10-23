-- 20-authority.sql — who may do what in the e2e data set (TASK-085, Step 18 precondition "approved state/authority").
--
-- The shipped matrix (db/seed, Blueprint Appendix A as far as AHDA has confirmed it) grants nobody the AHDA-side review,
-- approval or activation permissions: project-registration.md F-1 and its successors. Every integration suite adds them as
-- test grants on the shipped profiles. The e2e data set does not touch the shipped profiles: it adds three test profiles,
-- E2E-R02, E2E-R03 and E2E-R04, on the same base roles, and binds only the e2e people to them. The entity personas hold the
-- SHIPPED R04 and R08 profiles unchanged, so ADR-013's entity Project Manager is tested exactly as shipped.
--
-- E2E-R02 (ALL) is AHDA's reviewer, approver and activator: the union of the R02 test grants the integration hosts add.
-- E2E-R03 (DEPT) registers, reviews and raises governance for its department: the shipped R03 grants plus the hosts' R03 ones.
-- E2E-R04 (OWN) is an internal Project Manager: the shipped R04 grants with ENTITY read as OWN — an internal user has no entity
-- anchor, so the shipped ENTITY grants give them nothing (TASK-061 F-1, TASK-064) — plus the hosts' R04 additions.
--
-- On SIT this file is replaced by AHDA's approved matrix when it exists (docs/testing/e2e-suite.md F-2). Synthetic kinds:
-- e2e6 permission profile, e2e7 its version, e2e2 access relationship.
--
-- Idempotent: safe to re-run.

INSERT INTO identity_access.permission_profile (id, code, base_role_id, is_shipped_default, name_ar, name_en, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e6-4000-8000-000000000002', 'E2E-R02', '00000000-0000-4000-8000-000000000002', false, 'ملف اختبار المسارات — مدير المحفظة', 'E2E test authority — Portfolio Manager',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-e2e6-4000-8000-000000000003', 'E2E-R03', '00000000-0000-4000-8000-000000000003', false, 'ملف اختبار المسارات — مدير القطاع', 'E2E test authority — Sector Manager',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-e2e6-4000-8000-000000000004', 'E2E-R04', '00000000-0000-4000-8000-000000000004', false, 'ملف اختبار المسارات — مدير المشروع', 'E2E test authority — Project Manager',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en, updated_at = now();

INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                        published_by_user_id, published_at, created_at, created_by, updated_at, updated_by)
SELECT overlay(p.id::text placing 'e2e7' from 10 for 4)::uuid, p.id, 1, 'en', 'E2E data set (TASK-085)', 'PUBLISHED',
       '00000000-0000-4000-8000-0000000000ff', now(), now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.permission_profile p
WHERE p.code IN ('E2E-R02', 'E2E-R03', 'E2E-R04')
ON CONFLICT (id) DO NOTHING;

-- The grants: one per permission per version. Re-running replaces the set, so an edit here takes effect on the next provision.
DELETE FROM identity_access.permission_profile_grant
WHERE permission_profile_version_id IN ('00000000-e2e7-4000-8000-000000000002', '00000000-e2e7-4000-8000-000000000003', '00000000-e2e7-4000-8000-000000000004');

WITH wanted (version_id, permission_code, data_scope) AS (
    -- E2E-R02: the shipped R02 grants, then AHDA's side of every workflow at ALL.
    SELECT '00000000-e2e7-4000-8000-000000000002', p.code, g.data_scope
    FROM identity_access.permission_profile_grant g JOIN identity_access.permission p ON p.id = g.permission_id
    WHERE g.permission_profile_version_id = '00000000-0002-4000-8000-000000000002'
    UNION ALL
    SELECT '00000000-e2e7-4000-8000-000000000002', code, 'ALL' FROM unnest(ARRAY[
        'ACTIVITY_VIEW', 'APPROVAL_DECIDE', 'APPROVAL_VIEW', 'AUDIT_BUSINESS_VIEW', 'AUDIT_CONTEXT_VIEW', 'AUDIT_EXTERNAL_VIEW', 'AUDIT_FINANCIAL_VIEW',
        'CHANGE_REQUEST_IMPLEMENT', 'CHANGE_REQUEST_RAISE', 'CHANGE_REQUEST_REVIEW', 'CHANGE_REQUEST_VIEW',
        'CLOSEOUT_ACTIVATE', 'CLOSEOUT_REVIEW', 'CLOSEOUT_VIEW', 'CLOSEOUT_WAIVE',
        'CONCERN_ESCALATION_RESOLVE', 'CONCERN_VIEW', 'CONFIGURATION_VIEW',
        'DOCUMENT_MANAGE', 'DOCUMENT_UPLOAD', 'DOCUMENT_VIEW',
        'EXTERNAL_CONTRIBUTION_REVIEW', 'EXTERNAL_REQUEST_MANAGE', 'EXTERNAL_REQUEST_VIEW',
        'FINANCIAL_REVIEW', 'FINANCIAL_SOURCE_MANAGE', 'FINANCIAL_SUBMIT', 'FINANCIAL_VIEW',
        'KPI_MANAGE', 'KPI_RECORD', 'KPI_REVIEW', 'KPI_VIEW', 'MASTER_DATA_VIEW', 'MILESTONE_VIEW', 'ORGANIZATION_VIEW',
        'PROGRESS_REVIEW', 'PROGRESS_SUBMIT', 'PROGRESS_VIEW', 'PROJECT_ACTIVATE', 'PROJECT_REVIEW', 'PROJECT_VIEW',
        'REPORT_EXPORT', 'RISK_ACCEPT', 'RISK_ASSESS', 'RISK_MANAGE', 'RISK_REOPEN', 'RISK_VIEW',
        'SCHEDULE_EDIT', 'SCHEDULE_VIEW', 'SUSPENSION_ACTIVATE', 'SUSPENSION_REVIEW', 'SUSPENSION_VIEW',
        'TASK_MANAGE', 'TASK_UPDATE', 'TASK_VIEW']) AS code
    UNION ALL
    -- E2E-R03: the shipped R03 grants, then registration, review and raising for the department.
    SELECT '00000000-e2e7-4000-8000-000000000003', p.code, g.data_scope
    FROM identity_access.permission_profile_grant g JOIN identity_access.permission p ON p.id = g.permission_id
    WHERE g.permission_profile_version_id = '00000000-0002-4000-8000-000000000003'
    UNION ALL
    SELECT '00000000-e2e7-4000-8000-000000000003', code, 'DEPT' FROM unnest(ARRAY[
        'APPROVAL_DECIDE', 'APPROVAL_VIEW', 'CLOSEOUT_RAISE', 'DOCUMENT_UPLOAD', 'DOCUMENT_VIEW', 'KPI_RECORD', 'KPI_VIEW',
        'PROGRESS_REVIEW', 'PROGRESS_VIEW', 'PROJECT_REGISTER', 'PROJECT_REVIEW', 'PROJECT_VIEW', 'REPORT_EXPORT',
        'RISK_REOPEN', 'RISK_VIEW', 'SCHEDULE_VIEW', 'SUSPENSION_RAISE']) AS code
    UNION ALL
    -- The registration form's lists: classifications, governance profiles, regions, cities, departments and entities.
    -- Naming someone else as Project Manager searches users (ADM-002), a collection read only an ALL grant passes, and the
    -- shipped matrix gives USER_VIEW to R01 alone (docs/testing/e2e-suite.md F-2).
    SELECT '00000000-e2e7-4000-8000-000000000003', code, 'ALL' FROM unnest(ARRAY['CONFIGURATION_VIEW', 'MASTER_DATA_VIEW', 'ORGANIZATION_VIEW', 'USER_VIEW']) AS code
    UNION ALL
    -- UJ-012: a configuration version needs three people (master-data-configuration.md D-2: author, reviewer and publisher
    -- differ), and the shipped matrix gives CONFIGURATION_MANAGE to R01 alone, held only by e2e.admin. The Portfolio Manager
    -- authors the RISK_MATRIX draft, the Department Manager reviews it, the administrator publishes it with step-up (D-11).
    -- Configuration has no record scope (D-10), so ALL. The Project Manager (E2E-R04) keeps CONFIGURATION_VIEW only.
    SELECT v.version_id, 'CONFIGURATION_MANAGE', 'ALL'
    FROM unnest(ARRAY['00000000-e2e7-4000-8000-000000000002', '00000000-e2e7-4000-8000-000000000003']) AS v (version_id)
    UNION ALL
    -- E2E-R04: the shipped R04 grants with ENTITY read as OWN, then what a Project Manager records and assesses.
    SELECT '00000000-e2e7-4000-8000-000000000004', p.code, CASE g.data_scope WHEN 'ENTITY' THEN 'OWN' ELSE g.data_scope END
    FROM identity_access.permission_profile_grant g JOIN identity_access.permission p ON p.id = g.permission_id
    WHERE g.permission_profile_version_id = '00000000-0002-4000-8000-000000000004'
    UNION ALL
    SELECT '00000000-e2e7-4000-8000-000000000004', code, 'OWN' FROM unnest(ARRAY[
        'FINANCIAL_SUBMIT', 'KPI_MANAGE', 'KPI_RECORD', 'RISK_ACCEPT', 'RISK_ASSESS']) AS code
    UNION ALL
    SELECT '00000000-e2e7-4000-8000-000000000004', code, 'ALL' FROM unnest(ARRAY['CONFIGURATION_VIEW', 'MASTER_DATA_VIEW', 'ORGANIZATION_VIEW']) AS code
),
distinct_wanted AS (
    -- A permission listed twice keeps its first scope (the shipped one).
    SELECT DISTINCT ON (version_id, permission_code) version_id, permission_code, data_scope FROM wanted ORDER BY version_id, permission_code
)
INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
SELECT md5('e2e-grant:' || w.version_id || ':' || w.permission_code)::uuid, w.version_id::uuid, p.id, w.data_scope,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM distinct_wanted w JOIN identity_access.permission p ON p.code = w.permission_code;

-- Assignments. Shipped profile versions are 00000000-0002-4000-8000-0000000000nn for Rnn; the e2e ones are kind e2e7.
INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
SELECT a.id::uuid, a.user_id::uuid, a.version_id::uuid, a.department_id::uuid, a.entity_id::uuid,
       CASE WHEN a.entity_id IS NOT NULL THEN '00000000-e2e1-4000-8000-000000000002'::uuid END,
       now() - interval '1 day', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-e2e2-4000-8000-000000000011', '00000000-e2e1-4000-8000-000000000001', '00000000-0002-4000-8000-000000000001', NULL, NULL),
    ('00000000-e2e2-4000-8000-000000000021', '00000000-e2e1-4000-8000-000000000002', '00000000-e2e7-4000-8000-000000000002', NULL, NULL),
    ('00000000-e2e2-4000-8000-000000000031', '00000000-e2e1-4000-8000-000000000003', '00000000-e2e7-4000-8000-000000000003', '00000000-e2e4-4000-8000-000000000001', NULL),
    ('00000000-e2e2-4000-8000-000000000041', '00000000-e2e1-4000-8000-000000000004', '00000000-e2e7-4000-8000-000000000004', NULL, NULL),
    ('00000000-e2e2-4000-8000-000000000051', '00000000-e2e1-4000-8000-000000000005', '00000000-0002-4000-8000-000000000007', NULL, NULL),
    ('00000000-e2e2-4000-8000-000000000061', '00000000-e2e1-4000-8000-000000000006', '00000000-0002-4000-8000-000000000006', NULL, NULL),
    ('00000000-e2e2-4000-8000-000000000071', '00000000-e2e1-4000-8000-000000000007', '00000000-e2e7-4000-8000-000000000003', '00000000-e2e4-4000-8000-000000000002', NULL),
    -- ADR-013: the entity Project Manager holds the shipped R04 and R08 profiles on entity A.
    ('00000000-e2e2-4000-8000-000000000111', '00000000-e2e1-4000-8000-000000000011', '00000000-0002-4000-8000-000000000004', NULL, '00000000-e2e5-4000-8000-000000000001'),
    ('00000000-e2e2-4000-8000-000000000112', '00000000-e2e1-4000-8000-000000000011', '00000000-0002-4000-8000-000000000008', NULL, '00000000-e2e5-4000-8000-000000000001'),
    ('00000000-e2e2-4000-8000-000000000121', '00000000-e2e1-4000-8000-000000000012', '00000000-0002-4000-8000-000000000008', NULL, '00000000-e2e5-4000-8000-000000000001'),
    -- Entity B's Project Manager: the same shipped profiles, on entity B.
    ('00000000-e2e2-4000-8000-000000000131', '00000000-e2e1-4000-8000-000000000013', '00000000-0002-4000-8000-000000000004', NULL, '00000000-e2e5-4000-8000-000000000002'),
    ('00000000-e2e2-4000-8000-000000000132', '00000000-e2e1-4000-8000-000000000013', '00000000-0002-4000-8000-000000000008', NULL, '00000000-e2e5-4000-8000-000000000002'))
    AS a (id, user_id, version_id, department_id, entity_id)
ON CONFLICT (id) DO UPDATE SET user_id = EXCLUDED.user_id, permission_profile_version_id = EXCLUDED.permission_profile_version_id,
    department_id = EXCLUDED.department_id, external_entity_id = EXCLUDED.external_entity_id, sponsor_user_id = EXCLUDED.sponsor_user_id,
    ends_at = NULL, end_reason = NULL, status = 'ACTIVE', updated_at = now(), updated_by = EXCLUDED.updated_by;
