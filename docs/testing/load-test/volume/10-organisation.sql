-- 10-organisation.sql — the load test's organisation and people (TASK-086). The volume indexing-strategy.md §5.2 (TASK-026)
-- reviewed its query plans at: 13 departments, 40 external entities and 150 named users (PTBC-043, working baseline). The
-- e2e data set (src/frontend/e2e/stack/provision) already holds 10 people; this file adds 140, load.u001 to load.u140, so
-- the platform has the brief's 150. Every person signs in against the load stack's directory, whose image is generated from
-- the same numbering (username load.uNNN, subject id below), so the ids here must not change.
--
-- Applied once, after db/seed and the e2e provision files, on a freshly migrated database (docs/testing/load-test/volume/,
-- in file order, each with psql --single-transaction). Every file of this folder checks it has not run before and is a
-- no-op on a second run.
--
-- Synthetic identifiers: 00000000-<kind>-4000-8000-<n>, n the row number in decimal, zero-padded to 12 digits (as the e2e
-- data set writes its ids). Kinds of this folder:
--   10a1 user                   10a2 access relationship     10a3 directory subject       10a4 department
--   10a5 external entity        10a6 project                 10a7 risk                    10a8 risk assessment version
--   10a9 risk assessment impact 10aa management concern      10ab concern escalation      10ac approval instance
--   10ad approval task          10ae change request          10af suspension request      10b0 active suspension
--   10b1 document               10b2 document version        10b3 project schedule        10b4 project task
--   10b5 project milestone      10b6 milestone achievement   10b7 project baseline        10b8 reporting cycle
--   10b9 progress submission    10ba published progress snapshot                          10bb project health status
--   10bc schedule health status 10bd financial commitment    10be financial progress update
--   10bf published financial snapshot                        10c0 KPI definition          10c1 KPI assignment
--   10c2 KPI target version     10c3 KPI measurement         10c4 notification intent     10c5 notification delivery
--   10c6 completion case        10c7 closure case
--
-- People (job title, department and grants), mirroring 20-authority.sql's profiles:
--   001–010  Portfolio Manager       E2E-R02 (ALL)                            department LOAD-D01
--   011–036  Department Manager      E2E-R03 (DEPT) on their department       users 011+2k and 012+2k: LOAD-D(k+1), k = 0..12
--   037–111  Project Manager         E2E-R04 (OWN)                            LOAD-D(((n-37) % 13) + 1)
--   112–131  Entity Project Manager  shipped R04 and R08 on LOAD-E(n-111)     EXTERNAL, no department, sponsor load.u001 (ADR-013)
--   132–136  Executive               shipped R07                              LOAD-D01
--   137–140  Viewer                  shipped R06                              LOAD-D01
-- §5.2 made one user in fifteen DISABLED; here every person is ACTIVE, because each one is a virtual user of the load script.
-- External entities LOAD-E01..E40 are ACTIVE and sponsored by load.u001; only E01..E20 deliver projects (20-projects.sql).

SELECT NOT EXISTS (SELECT 1 FROM identity_access.department WHERE id = '00000000-10a4-4000-8000-000000000001') AS load_organisation_fresh \gset
\if :load_organisation_fresh

INSERT INTO identity_access.department (id, code, name_ar, name_en, directory_reference, is_active, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a4-4000-8000-' || lpad(d::text, 12, '0'))::uuid, 'LOAD-D' || lpad(d::text, 2, '0'),
       'إدارة الحمل ' || d, 'Load Department ' || lpad(d::text, 2, '0'), 'LOAD-D' || lpad(d::text, 2, '0'), true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM generate_series(1, 13) AS d;

-- load.u001 sponsors every entity, so it is written first, without an entity.
INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, department_id, preferred_language, status,
                                    created_at, created_by, updated_at, updated_by)
VALUES ('00000000-10a1-4000-8000-000000000001', 'INTERNAL', '00000000-10a3-4000-8000-000000000001', 'load.u001', 'Load User 001',
        'load.u001@pmplatform.local', 'Portfolio Manager', '00000000-10a4-4000-8000-000000000001', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff');

INSERT INTO identity_access.external_entity (id, code, name_ar, name_en, entity_type_item_id, status, sponsor_user_id, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a5-4000-8000-' || lpad(e::text, 12, '0'))::uuid, 'LOAD-E' || lpad(e::text, 2, '0'),
       'جهة الحمل ' || e, 'Load Entity ' || lpad(e::text, 2, '0'),
       (SELECT i.id FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'EXTERNAL_ENTITY_TYPE' AND i.code = (ARRAY['GOVERNMENT', 'PRIVATE_COMPANY', 'PUBLIC_AUTHORITY'])[e % 3 + 1]),
       'ACTIVE', '00000000-10a1-4000-8000-000000000001', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM generate_series(1, 40) AS e;

INSERT INTO identity_access."user" (id, user_type, directory_subject_id, username, display_name, email, job_title, department_id, external_entity_id,
                                    preferred_language, status, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a1-4000-8000-' || lpad(n::text, 12, '0'))::uuid,
       CASE WHEN n BETWEEN 112 AND 131 THEN 'EXTERNAL' ELSE 'INTERNAL' END,
       '00000000-10a3-4000-8000-' || lpad(n::text, 12, '0'),
       'load.u' || lpad(n::text, 3, '0'), 'Load User ' || lpad(n::text, 3, '0'), 'load.u' || lpad(n::text, 3, '0') || '@pmplatform.local',
       CASE WHEN n <= 10 THEN 'Portfolio Manager' WHEN n <= 36 THEN 'Department Manager' WHEN n <= 111 THEN 'Project Manager'
            WHEN n <= 131 THEN 'Entity Project Manager' WHEN n <= 136 THEN 'Executive' ELSE 'Viewer' END,
       CASE WHEN n BETWEEN 11 AND 36 THEN ('00000000-10a4-4000-8000-' || lpad(((n - 11) / 2 + 1)::text, 12, '0'))::uuid
            WHEN n BETWEEN 37 AND 111 THEN ('00000000-10a4-4000-8000-' || lpad(((n - 37) % 13 + 1)::text, 12, '0'))::uuid
            WHEN n BETWEEN 112 AND 131 THEN NULL
            ELSE '00000000-10a4-4000-8000-000000000001'::uuid END,
       CASE WHEN n BETWEEN 112 AND 131 THEN ('00000000-10a5-4000-8000-' || lpad((n - 111)::text, 12, '0'))::uuid END,
       'en', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM generate_series(2, 140) AS n;

-- Grants. Shipped profile versions are 00000000-0002-4000-8000-0000000000nn for Rnn; the e2e ones (20-authority.sql) kind e2e7.
-- Each person's grant is 10a2-<n>; an entity Project Manager's second grant, R08, is 10a2-<1000 + n>.
INSERT INTO identity_access.access_relationship (id, user_id, permission_profile_version_id, department_id, external_entity_id, sponsor_user_id, starts_at, status,
                                                 created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a2-4000-8000-' || lpad(g.grant_no::text, 12, '0'))::uuid, ('00000000-10a1-4000-8000-' || lpad(g.n::text, 12, '0'))::uuid,
       g.version_id::uuid, g.department_id, g.entity_id, CASE WHEN g.entity_id IS NOT NULL THEN '00000000-10a1-4000-8000-000000000001'::uuid END,
       now() - interval '730 days', 'ACTIVE', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (
    SELECT n, n AS grant_no,
           CASE WHEN n <= 10 THEN '00000000-e2e7-4000-8000-000000000002' WHEN n <= 36 THEN '00000000-e2e7-4000-8000-000000000003'
                WHEN n <= 111 THEN '00000000-e2e7-4000-8000-000000000004' WHEN n <= 131 THEN '00000000-0002-4000-8000-000000000004'
                WHEN n <= 136 THEN '00000000-0002-4000-8000-000000000007' ELSE '00000000-0002-4000-8000-000000000006' END AS version_id,
           CASE WHEN n BETWEEN 11 AND 36 THEN ('00000000-10a4-4000-8000-' || lpad(((n - 11) / 2 + 1)::text, 12, '0'))::uuid END AS department_id,
           CASE WHEN n BETWEEN 112 AND 131 THEN ('00000000-10a5-4000-8000-' || lpad((n - 111)::text, 12, '0'))::uuid END AS entity_id
    FROM generate_series(1, 140) AS n
    UNION ALL
    SELECT n, 1000 + n, '00000000-0002-4000-8000-000000000008', NULL, ('00000000-10a5-4000-8000-' || lpad((n - 111)::text, 12, '0'))::uuid
    FROM generate_series(112, 131) AS n) AS g;

\endif
