-- 02-seed-local-users.sql — local test users, one per role R01–R08 (TASK-014 acceptance criterion).
--
-- LOCAL STACK ONLY. These are synthetic accounts for a developer's docker-compose database. They are never
-- promoted to DEV, SIT, UAT or PROD (TASK-027's db/seed holds roles and master data, not people), have no
-- credential (sign-in is SSO, ADR-007; a local sign-in path is TASK-028's test directory), and use the reserved
-- pmplatform.local domain.
--
-- Synthetic identifiers: 00000000-<kind>-4000-8000-<n>, kind 0000 role, 0001 profile, 0002 profile version,
-- 0010 user, 0011 access relationship, 0020 department, 0021 external entity, 0030/0031 master data.
-- The SERVICE principal 00000000-0000-4000-8000-0000000000ff is the D-2 actor for every seeded row.
--
-- Idempotent: safe to re-run.

-- The seed actor (ERD: SERVICE is the non-human principal that lets created_by be not null on seeded rows).
INSERT INTO identity_access."user" (id, user_type, username, display_name, email, preferred_language, status, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0000-4000-8000-0000000000ff', 'SERVICE', 'svc.local-seed', 'Local seed (service principal)', 'svc.local-seed@pmplatform.local', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO NOTHING;

-- One department: the DEPT scope anchor for R03 and the home of every internal user.
INSERT INTO identity_access.department (id, code, name_ar, name_en, is_active, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0020-4000-8000-000000000001', 'DEPT-LOCAL', 'إدارة الاختبار المحلية', 'Local Test Department', true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en, updated_at = now(), updated_by = EXCLUDED.updated_by;

-- External-entity type is master data (external_entity.entity_type_item_id is not null): one catalogue, one item.
INSERT INTO master_data_config.master_data_catalogue (id, code, name_ar, name_en, allows_hierarchy, is_system, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0030-4000-8000-000000000001', 'EXTERNAL_ENTITY_TYPE', 'نوع الجهة الخارجية', 'External entity type', false, true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en, updated_at = now(), updated_by = EXCLUDED.updated_by;

INSERT INTO master_data_config.master_data_item (id, catalogue_id, code, label_ar, label_en, sort_order, is_system, lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0031-4000-8000-000000000001', '00000000-0030-4000-8000-000000000001', 'PRIVATE_COMPANY', 'شركة خاصة', 'Private company', 1, true, 'PUBLISHED', now(),
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, label_ar = EXCLUDED.label_ar, label_en = EXCLUDED.label_en, updated_at = now(), updated_by = EXCLUDED.updated_by;

-- Internal users R01–R07. R02 is the AHDA sponsor of the external entity below (ADR-013: named sponsor).
INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, department_id, preferred_language, status, created_at, created_by, updated_at, updated_by)
VALUES
    ('00000000-0010-4000-8000-000000000001', 'INTERNAL', 'local-r01', 'local.r01', 'Local R01 — System Administrator', 'local.r01@pmplatform.local', 'System Administrator', '00000000-0020-4000-8000-000000000001', 'en', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000002', 'INTERNAL', 'local-r02', 'local.r02', 'Local R02 — Portfolio Manager',     'local.r02@pmplatform.local', 'Portfolio Manager',    '00000000-0020-4000-8000-000000000001', 'ar', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000003', 'INTERNAL', 'local-r03', 'local.r03', 'Local R03 — Department Manager',    'local.r03@pmplatform.local', 'Department Manager',   '00000000-0020-4000-8000-000000000001', 'ar', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000004', 'INTERNAL', 'local-r04', 'local.r04', 'Local R04 — Project Manager',       'local.r04@pmplatform.local', 'Project Manager',      '00000000-0020-4000-8000-000000000001', 'ar', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000005', 'INTERNAL', 'local-r05', 'local.r05', 'Local R05 — Liaison',               'local.r05@pmplatform.local', 'Liaison',              '00000000-0020-4000-8000-000000000001', 'ar', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000006', 'INTERNAL', 'local-r06', 'local.r06', 'Local R06 — Viewer',                'local.r06@pmplatform.local', 'Viewer',               '00000000-0020-4000-8000-000000000001', 'en', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0010-4000-8000-000000000007', 'INTERNAL', 'local-r07', 'local.r07', 'Local R07 — Executive',             'local.r07@pmplatform.local', 'Executive',            '00000000-0020-4000-8000-000000000001', 'ar', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET
    user_type = EXCLUDED.user_type, directory_subject_id = EXCLUDED.directory_subject_id, username = EXCLUDED.username,
    display_name = EXCLUDED.display_name, email = EXCLUDED.email, job_title = EXCLUDED.job_title,
    department_id = EXCLUDED.department_id, preferred_language = EXCLUDED.preferred_language, status = EXCLUDED.status,
    updated_at = now(), updated_by = EXCLUDED.updated_by;

-- One external entity, sponsored by R02, then its R08 user (user_type EXTERNAL requires external_entity_id).
INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, sponsor_user_id, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0021-4000-8000-000000000001', 'ENT-LOCAL', 'الجهة الخارجية المحلية', 'Local External Entity', '00000000-0031-4000-8000-000000000001', 'ACTIVE', '00000000-0010-4000-8000-000000000002',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en, entity_type_item_id = EXCLUDED.entity_type_item_id,
    status = EXCLUDED.status, sponsor_user_id = EXCLUDED.sponsor_user_id, updated_at = now(), updated_by = EXCLUDED.updated_by;

INSERT INTO identity_access."user" (id, user_type, username, display_name, email, job_title, external_entity_id, preferred_language, status, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0010-4000-8000-000000000008', 'EXTERNAL', 'local.r08', 'Local R08 — External Entity User', 'local.r08@pmplatform.local', 'Entity Project Coordinator', '00000000-0021-4000-8000-000000000001', 'ar', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET
    user_type = EXCLUDED.user_type, username = EXCLUDED.username, display_name = EXCLUDED.display_name, email = EXCLUDED.email,
    job_title = EXCLUDED.job_title, external_entity_id = EXCLUDED.external_entity_id, preferred_language = EXCLUDED.preferred_language,
    status = EXCLUDED.status, updated_at = now(), updated_by = EXCLUDED.updated_by;

-- Assignments (ADR-018): each user is bound to version 1 of the shipped-default profile of role Rnn.
-- Scope anchors per Blueprint Section 10.1: R03 carries the DEPT anchor; R08 carries the ENTITY anchor and its
-- named AHDA sponsor (ADR-013). R04's per-project anchor waits for a project to exist (project_id stays null).
INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, external_entity_id, sponsor_user_id, starts_at, status, created_at, created_by, updated_at, updated_by)
SELECT
    overlay(u.id::text placing '0011' from 10 for 4)::uuid,
    u.id,
    overlay(r.id::text placing '0002' from 10 for 4)::uuid,
    CASE WHEN r.code = 'R03' THEN u.department_id END,
    CASE WHEN r.code = 'R08' THEN u.external_entity_id END,
    CASE WHEN r.code = 'R08' THEN '00000000-0010-4000-8000-000000000002'::uuid END,
    now(),
    'ACTIVE',
    now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access."user" u
JOIN identity_access.role r ON r.code = 'R0' || right(u.username, 1)
WHERE u.username ~ '^local\.r0[1-8]$'
ON CONFLICT (id) DO UPDATE SET
    user_id = EXCLUDED.user_id, permission_profile_version_id = EXCLUDED.permission_profile_version_id,
    department_id = EXCLUDED.department_id, external_entity_id = EXCLUDED.external_entity_id, sponsor_user_id = EXCLUDED.sponsor_user_id,
    status = EXCLUDED.status, updated_at = now(), updated_by = EXCLUDED.updated_by;
