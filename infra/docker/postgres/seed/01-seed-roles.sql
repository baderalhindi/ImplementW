-- 01-seed-roles.sql — the eight canonical roles R01–R08 and their shipped-default permission profiles (TASK-014).
--
-- Interim copy of what TASK-027 will own in db/seed (its deliverable: "the 8 canonical roles (R01-R08)"). Codes
-- R01–R08 are canonical (ERD F-080: shipped, undeletable, referenced by routing, landing dashboards and
-- notification matrices). The name_ar/name_en labels are PROVISIONAL: the Blueprint's Appendix A role list is not
-- in the repository, so the labels follow the role-based dashboard inventory DSH-001–008 (Administration,
-- Portfolio, Department, Project Manager, Liaison, Viewer, Executive, External Contributor) and the two roles the
-- workbook names outright — R01 System Administrator, R04 Project Manager. TASK-027 replaces the labels from the
-- controlled source; nothing here keys on a label.
--
-- Each role gets its shipped-default profile (ADR-018, TASK-110: "R01 to R08 as undeletable shipped defaults")
-- with one PUBLISHED version. Assignments in 02-seed-local-users.sql bind to that version. Grants are absent
-- until the permission catalogue exists (TASK-030/TASK-110).
--
-- The tables are created by the EF Core migrations (TASK-025); compose's `seed` service runs this file after its
-- `migrate` service, on every `up`. Idempotent: safe to re-run (`docker compose run --rm seed`).
-- All rows are attributed (D-2) to the SERVICE principal 00000000-0000-4000-8000-0000000000ff, created in
-- 02-seed-local-users.sql; created_by/updated_by carry no FK constraint (D-2), so the order does not matter.

INSERT INTO identity_access.role (id, code, name_ar, name_en, is_system, is_external_eligible, created_at, created_by, updated_at, updated_by)
VALUES
    ('00000000-0000-4000-8000-000000000001', 'R01', 'مدير النظام',          'System Administrator',   true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000002', 'R02', 'مدير المحفظة',          'Portfolio Manager',      true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000003', 'R03', 'مدير الإدارة',          'Department Manager',     true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000004', 'R04', 'مدير المشروع',          'Project Manager',        true, true,  now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000005', 'R05', 'ضابط الاتصال',          'Liaison',                true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000006', 'R06', 'مستعرض',                'Viewer',                 true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000007', 'R07', 'تنفيذي',                'Executive',              true, false, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
    ('00000000-0000-4000-8000-000000000008', 'R08', 'مستخدم جهة خارجية',     'External Entity User',   true, true,  now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO UPDATE SET
    code = EXCLUDED.code,
    name_ar = EXCLUDED.name_ar,
    name_en = EXCLUDED.name_en,
    is_system = EXCLUDED.is_system,
    is_external_eligible = EXCLUDED.is_external_eligible,
    updated_at = now(),
    updated_by = EXCLUDED.updated_by;

-- Shipped-default profile per role: id = role id with the second group set to 0001; code = <role code>-DEFAULT.
INSERT INTO identity_access.permission_profile (id, code, name_ar, name_en, base_role_id, is_shipped_default, created_at, created_by, updated_at, updated_by)
SELECT
    overlay(r.id::text placing '0001' from 10 for 4)::uuid,
    r.code || '-DEFAULT',
    r.name_ar || ' — الملف الافتراضي',
    r.name_en || ' — shipped default',
    r.id,
    true,
    now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.role r
WHERE r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
ON CONFLICT (id) DO UPDATE SET
    code = EXCLUDED.code,
    name_ar = EXCLUDED.name_ar,
    name_en = EXCLUDED.name_en,
    base_role_id = EXCLUDED.base_role_id,
    is_shipped_default = EXCLUDED.is_shipped_default,
    updated_at = now(),
    updated_by = EXCLUDED.updated_by;

-- Version 1 of each default profile, PUBLISHED: id = profile id with the second group set to 0002.
-- validated_by/published_by are left null: the author/reviewer/publisher separation (TASK-110) has no actors
-- in a seed, and a PUBLISHED row is immutable once published_at is set.
INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, lifecycle_state, published_at, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT
    overlay(p.id::text placing '0002' from 10 for 4)::uuid,
    p.id,
    1,
    'PUBLISHED',
    now(),
    'Shipped default (seed).',
    'en',
    now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.permission_profile p
WHERE p.is_shipped_default
ON CONFLICT (id) DO NOTHING;
