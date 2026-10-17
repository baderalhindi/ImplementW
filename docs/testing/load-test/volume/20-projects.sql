-- 20-projects.sql — the load test's portfolio: 3,000 projects (TASK-086; indexing-strategy.md §5.2: "20 per named user, 250
-- per department, 20 per manager, every second one entity-delivered, nine lifecycle states"), and the closure and suspension
-- records their lifecycle states stand on.
--
-- Project p = 1..3000 (id 10a6-p, kinds in 10-organisation.sql):
--   department      LOAD-D(((p-1) % 13) + 1): 230 or 231 per department (3,000 / 13; §5.2's "250" is 3,000 / 12)
--   odd p           delivered by LOAD-E((((p-1)/2) % 20) + 1), 75 per entity, ENTITY_MANAGED, managed by that entity's
--                   Project Manager (load.u112..u131)
--   even p          internal, AHDA_MANAGED, managed by load.u(037 + (((p/2)-1) % 75)), 20 per internal Project Manager
--   lifecycle       by a stable hash of p (hashint4, as RepresentativeVolume spreads §5.2's values): ACTIVE 2,302,
--                   COMPLETED 182, APPROVED_PLANNED 142, SUSPENDED 119, CLOSED 115, DRAFT 53, RETURNED 35, SUBMITTED 27,
--                   UNDER_REVIEW 25 (2,718 activated). ck_project_formal_project_id: a Formal Project ID (LOAD-Pnnnn) from APPROVED_PLANNED on.
--                   "Activated" (ACTIVE, SUSPENDED, COMPLETED, CLOSED; dashboards.md D-7) projects were activated 25 to 35
--                   months before the load, so each has two full years of monthly reporting (70-progress-financial.sql).
--   updated_at      a fixed permutation of p spread evenly over the two years before the load
--   title           bilingual: odd p Arabic, even p English (title_lang)
--   master data     classification E2E_INFRASTRUCTURE/E2E_DEVELOPMENT, governance profile LIGHT/STANDARD/FULL, region and city
--                   E2E_NORTH/E2E_SOUTH (30-master-data.sql, db/seed), each by hash
--
-- Dates are relative to the load (now(), one value per transaction): ids and distributions are identical on every run, and
-- the dates move with the day the volume is loaded, so "overdue" and "past review" mean the same thing whenever the test runs.
--
-- What each state needs, written as the owning module stores it (the guards are off: session_replication_role = replica,
-- as the repository's fixtures write stored facts; CHECK constraints still hold and validate-data-integrity.sql checks
-- foreign keys afterwards):
--   COMPLETED       an EFFECTED completion case (closure.check_project_closeout)
--   CLOSED          an EFFECTED completion case and an EFFECTED closure case of it
--   SUSPENDED       an EFFECTED SUSPEND request and an open active suspension (suspension.check_project_suspension)
-- Suspension requests total 600 (§5.2: one per five projects): those of the SUSPENDED projects, then ACTIVE projects in hash
-- order, by fours: an EFFECTED suspension with its EFFECTED resumption (the active suspension ended RESUMED), a REJECTED,
-- a WITHDRAWN and an UNDER_REVIEW suspension request; the remainder are REJECTED.

SELECT NOT EXISTS (SELECT 1 FROM project.project WHERE id = '00000000-10a6-4000-8000-000000000001') AS load_projects_fresh \gset
\if :load_projects_fresh

SET LOCAL session_replication_role = replica;

INSERT INTO project.project (id, formal_project_id, classification_item_id, department_id, external_entity_id, project_manager_user_id, lifecycle_state, revision_no,
                             governance_profile_item_id, participation_mode, registration_budget_sar, planned_start_date, planned_end_date, region_item_id, city_item_id,
                             activated_at, closed_at, description_lang, description, title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a6-4000-8000-' || lpad(p::text, 12, '0'))::uuid,
       CASE WHEN s.state NOT IN ('DRAFT', 'SUBMITTED', 'UNDER_REVIEW', 'RETURNED') THEN 'LOAD-P' || lpad(p::text, 4, '0') END,
       CASE WHEN (hashint4(p * 31 + 2) & 1048575) % 2 = 0 THEN '00000000-e2e9-4000-8000-000000000001'::uuid ELSE '00000000-e2e9-4000-8000-000000000002'::uuid END,
       ('00000000-10a4-4000-8000-' || lpad(((p - 1) % 13 + 1)::text, 12, '0'))::uuid,
       CASE WHEN p % 2 = 1 THEN ('00000000-10a5-4000-8000-' || lpad((((p - 1) / 2) % 20 + 1)::text, 12, '0'))::uuid END,
       m.manager_id,
       s.state,
       1,
       (SELECT i.id FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'GOVERNANCE_PROFILE' AND i.code = (ARRAY['STANDARD', 'STANDARD', 'FULL', 'LIGHT'])[(hashint4(p * 31 + 3) & 1048575) % 4 + 1]),
       CASE WHEN p % 2 = 1 THEN 'ENTITY_MANAGED' ELSE 'AHDA_MANAGED' END,
       (1000000 + ((hashint4(p * 31 + 4) & 1048575) % 490) * 100000)::numeric(18, 2),
       d.start_date, d.start_date + 900 + (hashint4(p * 31 + 5) & 1048575) % 1290,
       CASE WHEN p % 4 < 2 THEN '00000000-e2e9-4000-8000-000000000011'::uuid ELSE '00000000-e2e9-4000-8000-000000000012'::uuid END,
       CASE WHEN p % 4 < 2 THEN '00000000-e2e9-4000-8000-000000000021'::uuid ELSE '00000000-e2e9-4000-8000-000000000022'::uuid END,
       CASE WHEN s.state IN ('ACTIVE', 'SUSPENDED', 'COMPLETED', 'CLOSED') THEN d.activated_at END,
       CASE WHEN s.state = 'CLOSED' THEN now() - interval '20 days' - ((hashint4(p * 31 + 6) & 1048575) % 60) * interval '1 day' END,
       CASE WHEN p % 2 = 1 THEN 'ar' ELSE 'en' END,
       CASE WHEN p % 2 = 1 THEN 'وصف مشروع الحمل رقم ' || p || ' لاختبار الأداء.' ELSE 'Description of load project ' || p || ' for the performance test.' END,
       CASE WHEN p % 2 = 1 THEN 'ar' ELSE 'en' END,
       CASE WHEN p % 2 = 1 THEN 'مشروع الحمل ' || lpad(p::text, 4, '0') ELSE 'Load Project ' || lpad(p::text, 4, '0') END,
       d.created_at, m.manager_id, t.updated_at, m.manager_id
FROM generate_series(1, 3000) AS p
CROSS JOIN LATERAL (SELECT (hashint4(p * 31 + 1) & 1048575) % 100 AS h) AS hh
CROSS JOIN LATERAL (SELECT CASE WHEN hh.h < 2 THEN 'DRAFT' WHEN hh.h = 2 THEN 'SUBMITTED' WHEN hh.h = 3 THEN 'UNDER_REVIEW' WHEN hh.h = 4 THEN 'RETURNED'
                                WHEN hh.h < 10 THEN 'APPROVED_PLANNED' WHEN hh.h < 14 THEN 'SUSPENDED' WHEN hh.h < 20 THEN 'COMPLETED'
                                WHEN hh.h < 24 THEN 'CLOSED' ELSE 'ACTIVE' END AS state) AS s
CROSS JOIN LATERAL (SELECT CASE WHEN p % 2 = 1 THEN ('00000000-10a1-4000-8000-' || lpad((111 + ((p - 1) / 2) % 20 + 1)::text, 12, '0'))::uuid
                                ELSE ('00000000-10a1-4000-8000-' || lpad((37 + ((p / 2) - 1) % 75)::text, 12, '0'))::uuid END AS manager_id) AS m
CROSS JOIN LATERAL (SELECT now() - ((p::bigint * 7919) % 3000) * interval '21024 seconds' AS updated_at) AS t
CROSS JOIN LATERAL (SELECT now() - interval '760 days' - ((hashint4(p * 31 + 7) & 1048575) % 300) * interval '1 day' AS activated_at) AS a
CROSS JOIN LATERAL (SELECT (a.activated_at - interval '60 days')::date AS start_date,
                           least(a.activated_at - interval '90 days', t.updated_at - interval '1 day') AS created_at,
                           a.activated_at) AS d;

-- COMPLETED and CLOSED: the effected completion case (10c6-p), requested by the Project Manager.
INSERT INTO closure.completion_case (id, project_id, status, revision_no, requested_by_user_id, submitted_at, actual_project_completion_date, completion_narrative,
                                     completion_narrative_lang, effected_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c6-4000-8000-' || right(pr.id::text, 12))::uuid, pr.id, 'EFFECTED', 1, pr.project_manager_user_id,
       c.effected_at - interval '10 days', (c.effected_at - interval '12 days')::date, 'Delivered as planned; acceptance certificates on file.', 'en', c.effected_at,
       c.effected_at - interval '14 days', pr.project_manager_user_id, c.effected_at, '00000000-10a1-4000-8000-000000000001'
FROM project.project pr
CROSS JOIN LATERAL (SELECT coalesce(pr.closed_at - interval '45 days', least(pr.updated_at, now() - interval '5 days')) AS effected_at) AS c
WHERE pr.id::text LIKE '00000000-10a6-%' AND pr.lifecycle_state IN ('COMPLETED', 'CLOSED');

-- CLOSED: the effected closure case of that completion (10c7-p).
INSERT INTO closure.closure_case (id, project_id, completion_case_id, status, revision_no, requested_by_user_id, submitted_at, closure_narrative, closure_narrative_lang,
                                  effected_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c7-4000-8000-' || right(pr.id::text, 12))::uuid, pr.id, ('00000000-10c6-4000-8000-' || right(pr.id::text, 12))::uuid, 'EFFECTED', 1,
       pr.project_manager_user_id, pr.closed_at - interval '7 days', 'Obligations discharged; the project is closed.', 'en', pr.closed_at,
       pr.closed_at - interval '10 days', pr.project_manager_user_id, pr.closed_at, '00000000-10a1-4000-8000-000000000001'
FROM project.project pr
WHERE pr.id::text LIKE '00000000-10a6-%' AND pr.lifecycle_state = 'CLOSED';

-- Suspension requests (10af-n) and active suspensions (10b0-n): the plan of the header, 600 requests exactly.
-- The seed applies every file in one transaction, so a temporary table of an earlier file may still exist.
DROP TABLE IF EXISTS load_suspension_plan;
CREATE TEMPORARY TABLE load_suspension_plan ON COMMIT DROP AS
WITH projects AS (
    SELECT pr.id AS project_id, right(pr.id::text, 12)::integer AS p, pr.lifecycle_state, pr.project_manager_user_id AS manager_id, pr.activated_at
    FROM project.project pr WHERE pr.id::text LIKE '00000000-10a6-%'
),
suspended AS (
    SELECT project_id, p, manager_id, activated_at, 'SUSPENDED_NOW' AS kind FROM projects WHERE lifecycle_state = 'SUSPENDED'
),
budget AS (
    SELECT 600 - count(*) AS remaining FROM suspended
),
candidates AS (
    SELECT project_id, p, manager_id, activated_at, row_number() OVER (ORDER BY hashint4(p * 31 + 8) & 1048575, p) AS rn
    FROM projects WHERE lifecycle_state = 'ACTIVE'
),
chosen AS (
    SELECT c.project_id, c.p, c.manager_id, c.activated_at,
           CASE WHEN c.rn > 4 * (b.remaining / 5) THEN 'REJECTED'
                ELSE (ARRAY['RESUMED', 'REJECTED', 'WITHDRAWN', 'UNDER_REVIEW'])[(c.rn - 1) % 4 + 1] END AS kind
    FROM candidates c CROSS JOIN budget b
    WHERE c.rn <= 4 * (b.remaining / 5) + (b.remaining - 5 * (b.remaining / 5))
)
SELECT project_id, p, manager_id, activated_at, kind FROM suspended
UNION ALL
SELECT project_id, p, manager_id, activated_at, kind FROM chosen;

-- One row per request: the SUSPEND request of every planned project (10af-p), and the RESUME of a resumed one (10af-<10000 + p>).
INSERT INTO suspension.suspension_request (id, project_id, request_type, status, revision_no, requested_by_user_id, submitted_at, requested_effective_date,
                                           planned_resumption_date, effected_at, reason_lang, reason, created_at, created_by, updated_at, updated_by)
SELECT r.id, r.project_id, r.request_type, r.status, 1, r.manager_id, r.at - interval '5 days', (r.at - interval '1 day')::date,
       CASE WHEN r.request_type = 'SUSPEND' THEN (r.at + interval '90 days')::date END,
       CASE WHEN r.status = 'EFFECTED' THEN r.at END,
       'en', CASE WHEN r.request_type = 'SUSPEND' THEN 'Site access withdrawn by the authority pending a permit review.' ELSE 'The permit was renewed; work can resume.' END,
       r.at - interval '6 days', r.manager_id, r.at, '00000000-10a1-4000-8000-000000000001'
FROM (
    SELECT ('00000000-10af-4000-8000-' || lpad(p::text, 12, '0'))::uuid AS id, project_id, manager_id, 'SUSPEND' AS request_type,
           CASE kind WHEN 'SUSPENDED_NOW' THEN 'EFFECTED' WHEN 'RESUMED' THEN 'EFFECTED' ELSE kind END AS status,
           CASE kind WHEN 'SUSPENDED_NOW' THEN now() - ((hashint4(p * 31 + 9) & 1048575) % 120 + 10) * interval '1 day'
                     WHEN 'RESUMED' THEN now() - ((hashint4(p * 31 + 9) & 1048575) % 300 + 200) * interval '1 day'
                     WHEN 'UNDER_REVIEW' THEN now() - ((hashint4(p * 31 + 9) & 1048575) % 10 + 1) * interval '1 day'
                     ELSE now() - ((hashint4(p * 31 + 9) & 1048575) % 500 + 30) * interval '1 day' END AS at
    FROM load_suspension_plan
    UNION ALL
    SELECT ('00000000-10af-4000-8000-' || lpad((10000 + p)::text, 12, '0'))::uuid, project_id, manager_id, 'RESUME', 'EFFECTED',
           now() - ((hashint4(p * 31 + 9) & 1048575) % 300 + 200) * interval '1 day' + interval '60 days'
    FROM load_suspension_plan WHERE kind = 'RESUMED') AS r;

INSERT INTO suspension.active_suspension (id, project_id, suspension_request_id, started_at, ended_at, resumption_request_id, end_reason,
                                          created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b0-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid, l.project_id, s.id, s.effected_at, r.effected_at, r.id,
       CASE WHEN r.id IS NOT NULL THEN 'RESUMED' END,
       s.effected_at, '00000000-10a1-4000-8000-000000000001', coalesce(r.effected_at, s.effected_at), '00000000-10a1-4000-8000-000000000001'
FROM load_suspension_plan l
JOIN suspension.suspension_request s ON s.id = ('00000000-10af-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid
LEFT JOIN suspension.suspension_request r ON r.id = ('00000000-10af-4000-8000-' || lpad((10000 + l.p)::text, 12, '0'))::uuid
WHERE l.kind IN ('SUSPENDED_NOW', 'RESUMED');

\endif
