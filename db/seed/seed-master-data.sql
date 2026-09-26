-- seed-master-data.sql — the platform's shipped reference data (TASK-027). Record: docs/architecture/seed-data-and-integrity.md
--
-- What it holds:
--   1. the SERVICE principals (ERD D-2): the seed principal every seeded row is attributed to, and the directory-sync
--      principal a directory-sourced change to a user is attributed to (TASK-028, ADR-007);
--   2. the eight canonical roles R01–R08, each with its shipped-default permission profile and that profile's
--      PUBLISHED version 1 (ADR-018, TASK-110), and the permission catalogue with the grants of those versions
--      (TASK-030) — only the rows a controlled source fixes, since Blueprint Appendix A is not in the repository;
--   3. one master data catalogue for every master data reference in the ERD (ADM-020–029), with the items a
--      controlled source fixes: the external entity types (ERD), the three governance profiles (ADR-015) and the
--      four impact dimensions (ADR-011);
--   4. the twelve configuration families of the ERD;
--   5. the risk and issue scale, seeded generically (ADR-011, OQ-006): a DRAFT version 1 of RISK_MATRIX holding
--      five probability levels and five impact levels per dimension, labelled "Level n", with no descriptions and
--      no boundaries.
-- Every label is bilingual (ADR-012).
--
-- What it does not hold, and why (record §4):
--   - materiality bands: ADR-016 fixes three bands, but the band values wait on OQ-013;
--   - governance profile settings: cadence, band count and document control level are not stated anywhere, and the
--     assignment thresholds wait on OQ-014;
--   - rating labels and the 5×5 matrix: outstanding under OQ-006;
--   - items of the other catalogues (project classification, regions, document types, …): AHDA's values, listed in
--     Blueprint v2.0 ADM-020–029, which is not in the repository;
--   - project and record statuses: these are state columns with CHECK constraints (ERD §6), not master data.
--
-- Idempotent. Every insert is keyed on the row's business key (code, or its unique key), so a second run adds no
-- row and changes no row. The seed owns STRUCTURE — codes, is_system, a role's external eligibility, a profile's
-- base role — and puts it back if it has drifted. WORDING and VALUES — labels, sort order, configuration rows — are
-- inserted once and then belong to AHDA's administrators, so a deployment never reverts an edit made on ADM-020–029.
--
-- Ids of new rows are md5(<table>:<business key>) as a uuid, the same in every environment. The roles, profiles and
-- versions keep the ids the local stack has used since TASK-014 (00000000-000n-4000-8000-00000000000n).
--
-- Pure SQL, no psql meta-command, so the release image runs it unchanged. Run it in one transaction:
--   dotnet PMPlatform.Api.dll seed                                              (each environment, after `migrate`)
--   psql "<connection>" -X -q -v ON_ERROR_STOP=1 --single-transaction -f db/seed/seed-master-data.sql     (locally)

-- 1. The SERVICE principals. A SERVICE user is a non-human principal; it holds no role and cannot sign in. The
-- directory-sync principal's id is PMPlatform.Infrastructure's UserAccessRepository.DirectorySyncPrincipalId.
INSERT INTO identity_access."user" (id, user_type, username, display_name, email, preferred_language, status, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0000-4000-8000-0000000000ff', 'SERVICE', 'svc.platform-seed', 'Platform seed (service principal)', 'svc.platform-seed@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fe', 'SERVICE', 'svc.directory-sync', 'Directory synchronisation (service principal)', 'svc.directory-sync@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO NOTHING;

-- 2. Roles R01–R08. Codes are canonical (ERD F-080). The labels are PROVISIONAL (record F-1): Blueprint Appendix A
-- is not in the repository, so they follow the role dashboards DSH-001–008. R04 and R08 may be held by an external
-- entity's people (ADR-013).
INSERT INTO identity_access.role AS r (id, code, name_ar, name_en, is_system, is_external_eligible, created_at, created_by, updated_at, updated_by)
SELECT v.id::uuid, v.code, v.name_ar, v.name_en, true, v.is_external_eligible,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-0000-4000-8000-000000000001', 'R01', 'مدير النظام',      'System Administrator', false),
    ('00000000-0000-4000-8000-000000000002', 'R02', 'مدير المحفظة',     'Portfolio Manager',    false),
    ('00000000-0000-4000-8000-000000000003', 'R03', 'مدير الإدارة',     'Department Manager',   false),
    ('00000000-0000-4000-8000-000000000004', 'R04', 'مدير المشروع',     'Project Manager',      true),
    ('00000000-0000-4000-8000-000000000005', 'R05', 'ضابط الاتصال',     'Liaison',              false),
    ('00000000-0000-4000-8000-000000000006', 'R06', 'مستعرض',           'Viewer',               false),
    ('00000000-0000-4000-8000-000000000007', 'R07', 'تنفيذي',           'Executive',            false),
    ('00000000-0000-4000-8000-000000000008', 'R08', 'مستخدم جهة خارجية', 'External Entity User', true)
) AS v (id, code, name_ar, name_en, is_external_eligible)
ON CONFLICT (code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    is_external_eligible = EXCLUDED.is_external_eligible,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (r.is_system, r.is_external_eligible) IS DISTINCT FROM (EXCLUDED.is_system, EXCLUDED.is_external_eligible);

-- The shipped-default profile of each role: code <role>-DEFAULT, id = the role's id with the second group 0001.
INSERT INTO identity_access.permission_profile AS p (id, code, name_ar, name_en, base_role_id, is_shipped_default, created_at, created_by, updated_at, updated_by)
SELECT overlay(r.id::text placing '0001' from 10 for 4)::uuid, r.code || '-DEFAULT',
       r.name_ar || ' — الملف الافتراضي', r.name_en || ' — shipped default', r.id, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.role r
WHERE r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
ON CONFLICT (code) DO UPDATE SET
    base_role_id = EXCLUDED.base_role_id,
    is_shipped_default = EXCLUDED.is_shipped_default,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (p.base_role_id, p.is_shipped_default) IS DISTINCT FROM (EXCLUDED.base_role_id, EXCLUDED.is_shipped_default);

-- Version 1 of each shipped-default profile, PUBLISHED, so an assignment has a version to bind to (ADR-018). Never
-- updated: a PUBLISHED version is immutable.
INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, lifecycle_state, published_at, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT overlay(p.id::text placing '0002' from 10 for 4)::uuid, p.id, 1, 'PUBLISHED', now(), 'Shipped default (seed).', 'en',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.permission_profile p
JOIN identity_access.role r ON r.id = p.base_role_id
WHERE p.code = r.code || '-DEFAULT'
  AND r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
ON CONFLICT (permission_profile_id, version_no) DO NOTHING;

-- The permission catalogue (ERD F-081) and the grants of the shipped-default versions: PMPlatform.Application's
-- PermissionCatalogue, row for row (SeedDataTests checks it). Only rows a controlled source fixes for all eight roles:
-- ADM-041 is R01's (TASK-028); ADR-019 grants Personalize Layout and Compose Report to R02, R03 and R07, OWN because
-- each acts on the holder's own layout or report definition. Blueprint Appendix A, the rest of the matrix, is not in
-- the repository (record F-1).
--
-- The grants go into version 1 because no environment has run this seed with version 1 in use (none is provisioned).
-- Once one has, a change to the shipped grants is a new version and a migration of the assignments (TASK-110), not
-- an edit here: a PUBLISHED version is immutable (seed record F-12).
INSERT INTO identity_access.permission AS p (id, code, name_ar, name_en, permission_group, is_privileged, created_at, created_by, updated_at, updated_by)
SELECT md5('permission:' || v.code)::uuid, v.code, v.name_ar, v.name_en, v.permission_group, v.is_privileged,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('IDENTITY_INTEGRATION_MANAGE', 'إدارة تكامل الهوية', 'Manage identity integration', 'IDENTITY_ACCESS', true),
    ('LAYOUT_PERSONALIZE',          'تخصيص التخطيط',      'Personalize layout',          'DASHBOARDS',      false),
    ('REPORT_COMPOSE',              'إعداد التقارير',      'Compose report',              'REPORTS',         false)
) AS v (code, name_ar, name_en, permission_group, is_privileged)
ON CONFLICT (code) DO UPDATE SET
    permission_group = EXCLUDED.permission_group,
    is_privileged = EXCLUDED.is_privileged,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (p.permission_group, p.is_privileged) IS DISTINCT FROM (EXCLUDED.permission_group, EXCLUDED.is_privileged);

INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
SELECT md5('permission_profile_grant:' || g.role_code || ':' || g.permission_code)::uuid, v.id, p.id, g.data_scope,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('R01', 'IDENTITY_INTEGRATION_MANAGE', 'ALL'),
    ('R02', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R02', 'REPORT_COMPOSE',              'OWN'),
    ('R03', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R03', 'REPORT_COMPOSE',              'OWN'),
    ('R07', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R07', 'REPORT_COMPOSE',              'OWN')
) AS g (role_code, permission_code, data_scope)
JOIN identity_access.role r ON r.code = g.role_code
JOIN identity_access.permission_profile pp ON pp.base_role_id = r.id AND pp.code = r.code || '-DEFAULT'
JOIN identity_access.permission_profile_version v ON v.permission_profile_id = pp.id AND v.version_no = 1
JOIN identity_access.permission p ON p.code = g.permission_code
ON CONFLICT (permission_profile_version_id, permission_id) DO NOTHING;

-- 3. Master data catalogues: one per master data reference in the ERD. validate-data-integrity.sql checks that each
-- reference column points at an item of its catalogue; the two files name the same codes.
INSERT INTO master_data_config.master_data_catalogue AS c (id, code, name_ar, name_en, is_system, created_at, created_by, updated_at, updated_by)
SELECT md5('master_data_catalogue:' || v.code)::uuid, v.code, v.name_ar, v.name_en, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('EXTERNAL_ENTITY_TYPE',   'نوع الجهة الخارجية',      'External entity type'),
    ('GOVERNANCE_PROFILE',     'ملف الحوكمة',             'Governance profile'),
    ('IMPACT_DIMENSION',       'بُعد الأثر',               'Impact dimension'),
    ('DATA_CLASSIFICATION',    'تصنيف البيانات',          'Data classification'),
    ('DOCUMENT_CONTROL_LEVEL', 'مستوى ضبط الوثائق',       'Document control level'),
    ('PROJECT_CLASSIFICATION', 'تصنيف المشروع',           'Project classification'),
    ('REGION',                 'المنطقة',                 'Region'),
    ('CITY',                   'المدينة',                 'City'),
    ('WORKING_CALENDAR',       'تقويم العمل',             'Working calendar'),
    ('MILESTONE_CATEGORY',     'فئة المعلم الرئيسي',      'Milestone category'),
    ('EVIDENCE_TYPE',          'نوع الدليل',              'Evidence type'),
    ('DOCUMENT_TYPE',          'نوع الوثيقة',             'Document type'),
    ('PRIORITY',               'الأولوية',                'Priority'),
    ('RISK_CATEGORY',          'فئة الخطر',               'Risk category'),
    ('CONCERN_CATEGORY',       'فئة المشكلة أو التحدي',   'Issue and challenge category'),
    ('CONCERN_SEVERITY',       'خطورة المشكلة أو التحدي', 'Issue and challenge severity'),
    ('CONTRIBUTION_TYPE',      'نوع المساهمة',            'Contribution type'),
    ('UPDATE_REQUEST_TYPE',    'نوع طلب التحديث',         'Update request type'),
    ('ETIMAD_COST_CATEGORY',   'فئة التكلفة في اعتماد',   'Etimad cost category'),
    ('KPI_UNIT',               'وحدة قياس المؤشر',        'KPI unit'),
    ('MEASUREMENT_FREQUENCY',  'دورية القياس',            'Measurement frequency')
) AS v (code, name_ar, name_en)
ON CONFLICT (code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE NOT c.is_system;

-- The items a controlled source fixes, PUBLISHED so forms can offer them. Reviewer and publisher stay null: the
-- author/reviewer/publisher separation (TASK-110) has no people in a seed.
INSERT INTO master_data_config.master_data_item AS i (id, catalogue_id, code, label_ar, label_en, sort_order, is_system, lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
SELECT md5('master_data_item:' || v.catalogue_code || ':' || v.code)::uuid, c.id, v.code, v.label_ar, v.label_en, v.sort_order, true, 'PUBLISHED', now(),
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    -- ERD identity_access.external_entity.entity_type_item_id
    ('EXTERNAL_ENTITY_TYPE', 'GOVERNMENT',       'جهة حكومية',       'Government',       1),
    ('EXTERNAL_ENTITY_TYPE', 'PUBLIC_AUTHORITY', 'هيئة عامة',        'Public authority', 2),
    ('EXTERNAL_ENTITY_TYPE', 'PRIVATE_COMPANY',  'شركة خاصة',        'Private company',  3),
    -- ADR-015: three governance profiles
    ('GOVERNANCE_PROFILE',   'LIGHT',            'مبسّط',            'Light',            1),
    ('GOVERNANCE_PROFILE',   'STANDARD',         'قياسي',            'Standard',         2),
    ('GOVERNANCE_PROFILE',   'FULL',             'شامل',             'Full',             3),
    -- ADR-011: cost, schedule, reputation and at least one operational dimension, shared by risks and issues
    ('IMPACT_DIMENSION',     'COST',             'التكلفة',          'Cost',             1),
    ('IMPACT_DIMENSION',     'SCHEDULE',         'الجدول الزمني',    'Schedule',         2),
    ('IMPACT_DIMENSION',     'REPUTATION',       'السمعة',           'Reputation',       3),
    ('IMPACT_DIMENSION',     'OPERATIONAL',      'التشغيل',          'Operational',      4)
) AS v (catalogue_code, code, label_ar, label_en, sort_order)
JOIN master_data_config.master_data_catalogue c ON c.code = v.catalogue_code
ON CONFLICT (catalogue_id, code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE NOT i.is_system;

-- 4. Configuration families (ERD MasterDataConfig). A family's value keys are fixed in code, so the family is
-- platform-owned; its versions are AHDA's.
INSERT INTO master_data_config.configuration_family (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
SELECT md5('configuration_family:' || v.code)::uuid, v.code, v.name_ar, v.name_en,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('GOVERNANCE_PROFILE',   'ملفات الحوكمة',              'Governance profiles'),
    ('MATERIALITY_BAND',     'نطاقات الأهمية النسبية للتغيير', 'Change materiality bands'),
    ('RISK_MATRIX',          'مصفوفة المخاطر',             'Risk matrix'),
    ('APPROVAL_AUTHORITY',   'صلاحيات الاعتماد',           'Approval authority'),
    ('NOTIFICATION_ROUTING', 'توجيه الإشعارات',            'Notification routing'),
    ('KPI_POLICY',           'سياسة مؤشرات الأداء',        'KPI policy'),
    ('PARTICIPATION',        'المشاركة الخارجية',          'External participation'),
    ('EVIDENCE_POLICY',      'سياسة الأدلة',               'Evidence policy'),
    ('FIELD_CLASSIFICATION', 'تصنيف الحقول',               'Field classification'),
    ('REPORT_RULES',         'قواعد التقارير',             'Report rules'),
    ('DASHBOARD_RULES',      'قواعد لوحات المعلومات',      'Dashboard rules'),
    ('WORKFLOW_POLICY',      'سياسة سير العمل',            'Workflow policy')
) AS v (code, name_ar, name_en)
ON CONFLICT (code) DO NOTHING;

-- 5. The risk and issue scale, generically (ADR-011; OQ-006). Version 1 of RISK_MATRIX stays DRAFT: resolution
-- reads PUBLISHED versions only and fails closed without one (TASK-034), so nothing resolves against a scale AHDA
-- has not approved. AHDA adds the descriptions, boundaries, rating labels and 5×5 mapping on FG-04 and publishes.
INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT md5('configuration_version:RISK_MATRIX:1')::uuid, f.id, 1, 'DRAFT',
       'Generic five-level scale (seed). Level descriptions, boundaries, rating labels and the 5x5 mapping are outstanding (OQ-006).', 'en',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f
WHERE f.code = 'RISK_MATRIX'
ON CONFLICT (configuration_family_id, version_no) DO NOTHING;

-- Levels are added to the seeded version only while it is DRAFT; once AHDA validates it, the seed leaves it alone.
INSERT INTO master_data_config.probability_level_definition (id, configuration_version_id, level, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('probability_level_definition:RISK_MATRIX:1:' || l)::uuid, v.id, l, 'المستوى ' || l, 'Level ' || l,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_version v
CROSS JOIN generate_series(1, 5) AS l
WHERE v.id = md5('configuration_version:RISK_MATRIX:1')::uuid
  AND v.lifecycle_state = 'DRAFT'
ON CONFLICT (configuration_version_id, level) DO NOTHING;

INSERT INTO master_data_config.impact_level_definition (id, configuration_version_id, impact_dimension_item_id, level, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('impact_level_definition:RISK_MATRIX:1:' || d.code || ':' || l)::uuid, v.id, d.id, l, 'المستوى ' || l, 'Level ' || l,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_version v
CROSS JOIN master_data_config.master_data_item d
JOIN master_data_config.master_data_catalogue c ON c.id = d.catalogue_id
CROSS JOIN generate_series(1, 5) AS l
WHERE v.id = md5('configuration_version:RISK_MATRIX:1')::uuid
  AND v.lifecycle_state = 'DRAFT'
  AND c.code = 'IMPACT_DIMENSION'
  AND d.code IN ('COST', 'SCHEDULE', 'REPUTATION', 'OPERATIONAL')
ON CONFLICT (configuration_version_id, impact_dimension_item_id, level) DO NOTHING;
