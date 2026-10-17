-- 10-people.sql — the e2e suite's organisation and people (TASK-085, Step 18 TD-01: authorized, out-of-scope and external
-- identities). Synthetic only; every person signs in against the e2e stack's directory (e2e/stack/ldap/e2e-people.ldif).
--
-- Two departments (the second is the out-of-scope one) and two external entities, A and B (ADR-013: B's people must
-- never reach A's projects). Synthetic identifiers: 00000000-<kind>-4000-8000-<n>, kind e2e1 user, e2e3 directory subject,
-- e2e4 department, e2e5 external entity. Access relationships are 20-authority.sql's.
--
-- Idempotent: safe to re-run.

INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, is_active, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e4-4000-8000-000000000001', 'DEPT-E2E', 'إدارة اختبار المسارات', 'E2E Journeys Department', 'DEPT-E2E', true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-e2e4-4000-8000-000000000002', 'DEPT-E2E-OTHER', 'إدارة اختبار أخرى', 'E2E Other Department', 'DEPT-E2E-OTHER', true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en,
    directory_reference = EXCLUDED.directory_reference, is_active = true, updated_at = now(), updated_by = EXCLUDED.updated_by;

INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, department_id, preferred_language, status, created_at, created_by, updated_at, updated_by)
SELECT p.id::uuid, 'INTERNAL', p.subject::uuid, p.username, p.display_name, p.username || '@pmplatform.local', p.title, p.department::uuid, 'en', 'ACTIVE',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-e2e1-4000-8000-000000000001', '00000000-e2e3-4000-8000-000000000001', 'e2e.admin', 'E2E Administrator', 'System Administrator', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000002', '00000000-e2e3-4000-8000-000000000002', 'e2e.portfolio', 'E2E Portfolio Manager', 'Portfolio Manager', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000003', '00000000-e2e3-4000-8000-000000000003', 'e2e.department', 'E2E Department Manager', 'Department Manager', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000004', '00000000-e2e3-4000-8000-000000000004', 'e2e.pm', 'E2E Project Manager', 'Project Manager', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000005', '00000000-e2e3-4000-8000-000000000005', 'e2e.executive', 'E2E Executive', 'Executive', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000006', '00000000-e2e3-4000-8000-000000000006', 'e2e.viewer', 'E2E Viewer', 'Viewer', '00000000-e2e4-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000007', '00000000-e2e3-4000-8000-000000000007', 'e2e.other-department', 'E2E Other Department Manager', 'Department Manager', '00000000-e2e4-4000-8000-000000000002'))
    AS p (id, subject, username, display_name, title, department)
ON CONFLICT (id) DO UPDATE SET user_type = EXCLUDED.user_type, directory_subject_id = EXCLUDED.directory_subject_id, username = EXCLUDED.username,
    display_name = EXCLUDED.display_name, email = EXCLUDED.email, job_title = EXCLUDED.job_title, department_id = EXCLUDED.department_id,
    status = 'ACTIVE', updated_at = now(), updated_by = EXCLUDED.updated_by;

-- Entity A delivers the e2e projects; entity B is the other entity. Both sponsored by the e2e Portfolio Manager (ADR-013).
INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, sponsor_user_id, created_at, created_by, updated_at, updated_by)
SELECT e.id::uuid, e.code, e.name_ar, e.name_en,
       (SELECT i.id FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'EXTERNAL_ENTITY_TYPE' AND i.code = e.entity_type),
       'ACTIVE', '00000000-e2e1-4000-8000-000000000002', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES ('00000000-e2e5-4000-8000-000000000001', 'ENT-E2E-A', 'جهة الاختبار أ', 'E2E Entity A', 'GOVERNMENT'),
             ('00000000-e2e5-4000-8000-000000000002', 'ENT-E2E-B', 'جهة الاختبار ب', 'E2E Entity B', 'PRIVATE_COMPANY'))
    AS e (id, code, name_ar, name_en, entity_type)
ON CONFLICT (id) DO UPDATE SET code = EXCLUDED.code, name_ar = EXCLUDED.name_ar, name_en = EXCLUDED.name_en, entity_type_item_id = EXCLUDED.entity_type_item_id,
    status = 'ACTIVE', sponsor_user_id = EXCLUDED.sponsor_user_id, updated_at = now(), updated_by = EXCLUDED.updated_by;

INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, external_entity_id, preferred_language, status, created_at, created_by, updated_at, updated_by)
SELECT p.id::uuid, 'EXTERNAL', p.subject::uuid, p.username, p.display_name, p.username || '@pmplatform.local', p.title, p.entity::uuid, 'en', 'ACTIVE',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-e2e1-4000-8000-000000000011', '00000000-e2e3-4000-8000-000000000011', 'e2e.entity-pm', 'E2E Entity Project Manager', 'Entity Project Manager', '00000000-e2e5-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000012', '00000000-e2e3-4000-8000-000000000012', 'e2e.entity-a', 'E2E Entity A Contributor', 'Entity Project Coordinator', '00000000-e2e5-4000-8000-000000000001'),
    ('00000000-e2e1-4000-8000-000000000013', '00000000-e2e3-4000-8000-000000000013', 'e2e.entity-b', 'E2E Entity B Project Manager', 'Entity Project Manager', '00000000-e2e5-4000-8000-000000000002'))
    AS p (id, subject, username, display_name, title, entity)
ON CONFLICT (id) DO UPDATE SET user_type = EXCLUDED.user_type, directory_subject_id = EXCLUDED.directory_subject_id, username = EXCLUDED.username,
    display_name = EXCLUDED.display_name, email = EXCLUDED.email, job_title = EXCLUDED.job_title, external_entity_id = EXCLUDED.external_entity_id,
    status = 'ACTIVE', updated_at = now(), updated_by = EXCLUDED.updated_by;
