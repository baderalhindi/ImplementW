-- 30-risks-concerns.sql — WF-06 risks and WF-07 issues and challenges at indexing-strategy.md §5.2's volume (TASK-086):
-- SCR-080 Risk Register, SCR-081 Critical Risks, SCR-083/085 Issue and Challenge Registers, SCR-087 Escalations, and FG-01's
-- RISK.RISK_EXPOSURE and FG-02's MANAGEMENT_CONCERN.OPEN_CONCERNS read these rows.
--
-- risk.risk                        45,000  15 per project (k = 1..15, n = (p-1)*15 + k, id 10a7-n), owned by the project's
--                                          Project Manager; k divisible by 3 CLOSED (a third, 15,000), the rest ASSESSED,
--                                          TREATMENT or MONITORING by hash; an open risk's next review is due between 30 days
--                                          ago and 90 days ahead, so about a quarter are past review
-- risk.risk_assessment_version     90,000  2 per risk (10a8-<2n-1>, 10a8-<2n>), against 50-risk-matrix.sql's PUBLISHED RISK_MATRIX
--                                          version, each cell's rating exactly as that matrix maps it. One assessment in twenty
--                                          is HIGH, the matrix's top rating ("critical", §5.2): version 2 of one risk in ten (n % 10 = 0),
--                                          so 4,500 risks are currently critical; the others are LOW or MEDIUM
-- risk.risk_assessment_impact      90,000  one per assessment, on one IMPACT_DIMENSION at the overall impact level (§5.2 does
--                                          not size it; a detail view reads it)
-- management_concern.management_concern
--                                  24,000  8 per project (k = 1..8, n = (p-1)*8 + k, id 10aa-n); a quarter CHALLENGE by hash, the
--                                          rest ISSUE. Concerns 1..6 carry a resolution and its MANAGEMENT_CONCERN_RESOLUTION
--                                          approval (40-change-approval.sql, instance (p-1)*10 + k + 4): APPROVED → RESOLVED
--                                          or CLOSED, REJECTED or RETURNED → IN_PROGRESS, PENDING (concern 6 of every fifth
--                                          project) → PENDING_VALIDATION. Concerns 7 and 8 are open (OPEN, ASSIGNED,
--                                          IN_PROGRESS). No severity: severity is optional and its configuration is not
--                                          published by the data set
-- management_concern.concern_escalation
--                                   2,400  one per ten concerns: concerns 7 and 8 of projects with p % 5 in (1, 2), to R03
--                                          (WORKFLOW_POLICY's CONCERN_ESCALATION_ROLE); a fifth OPEN (concern 8 of p % 25 in
--                                          1, 6, 11, 16: 480), the rest RESOLVED by the department's first Department Manager,
--                                          or WITHDRAWN
--
-- The approval outcome of instance i is fixed by one rule, written here and in 40-change-approval.sql: PENDING when
-- i % 50 = 0; otherwise REJECTED when hashint4(i * 31 + 1) & 1048575 is 0 mod 20, RETURNED when 1 mod 20, else APPROVED.
--
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts; CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM risk.risk WHERE id = '00000000-10a7-4000-8000-000000000001') AS load_risks_fresh \gset
\if :load_risks_fresh

SET LOCAL session_replication_role = replica;

INSERT INTO risk.risk (id, project_id, risk_category_item_id, owner_user_id, status, identified_date, next_review_date, closed_at, closed_by_user_id, reopened_count,
                       closure_rationale_lang, closure_rationale, description_lang, description, title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a7-4000-8000-' || lpad(r.n::text, 12, '0'))::uuid, pr.id,
       CASE WHEN (hashint4(r.n * 31 + 1) & 1048575) % 2 = 0 THEN '00000000-e2e9-4000-8000-000000000041'::uuid ELSE '00000000-e2e9-4000-8000-000000000042'::uuid END,
       pr.project_manager_user_id, s.status, t.created_at::date,
       CASE WHEN s.status <> 'CLOSED' THEN current_date - 30 + (hashint4(r.n * 31 + 2) & 1048575) % 120 END,
       CASE WHEN s.status = 'CLOSED' THEN t.updated_at END, CASE WHEN s.status = 'CLOSED' THEN pr.project_manager_user_id END, 0,
       CASE WHEN s.status = 'CLOSED' THEN 'en' END, CASE WHEN s.status = 'CLOSED' THEN 'The exposure has passed; no residual risk remains.' END,
       pr.title_lang, CASE WHEN pr.title_lang = 'ar' THEN 'وصف الخطر ' || r.k || ' في المشروع.' ELSE 'Description of risk ' || r.k || ' on the project.' END,
       pr.title_lang, CASE WHEN pr.title_lang = 'ar' THEN 'خطر الحمل ' || r.n ELSE 'Load risk ' || r.n END,
       t.created_at, pr.project_manager_user_id, t.updated_at, pr.project_manager_user_id
FROM generate_series(1, 3000) AS p
JOIN project.project pr ON pr.id = ('00000000-10a6-4000-8000-' || lpad(p::text, 12, '0'))::uuid
CROSS JOIN LATERAL (SELECT k, (p - 1) * 15 + k AS n FROM generate_series(1, 15) AS k) AS r
CROSS JOIN LATERAL (SELECT CASE WHEN r.k % 3 = 0 THEN 'CLOSED'
                                ELSE (ARRAY['ASSESSED', 'TREATMENT', 'MONITORING'])[(hashint4(r.n * 31 + 3) & 1048575) % 3 + 1] END AS status) AS s
CROSS JOIN LATERAL (SELECT now() - ((r.n::bigint * 7919) % 45000) * interval '1401 seconds' AS updated_at) AS u
CROSS JOIN LATERAL (SELECT u.updated_at, u.updated_at - ((hashint4(r.n * 31 + 4) & 1048575) % 200) * interval '1 day' - interval '1 hour' AS created_at) AS t;

-- Two assessments per risk. HIGH (4 × 4 = 16 ≥ 15) for version 2 of one risk in ten; otherwise MEDIUM (2 × 3, 3 × 3) or LOW
-- (1 × 2, 2 × 2), as 50-risk-matrix.sql maps them.
INSERT INTO risk.risk_assessment_version (id, risk_id, version_no, assessed_at, assessed_by_user_id, matrix_configuration_version_id, probability_level,
                                          overall_impact_level, risk_rating_definition_id, rationale_lang, rationale, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a8-4000-8000-' || lpad((2 * a.n - 2 + a.version_no)::text, 12, '0'))::uuid, a.risk_id, a.version_no, a.assessed_at, a.owner_id,
       '00000000-e2e8-4000-8000-000000000601', c.probability, c.impact, md5('e2e-risk-rating:' || c.rating)::uuid,
       'en', 'Assessed at the periodic risk review.', a.assessed_at, a.owner_id, a.assessed_at, a.owner_id
FROM (
    SELECT right(r.id::text, 12)::integer AS n, r.id AS risk_id, v.version_no, r.owner_user_id AS owner_id,
           CASE v.version_no WHEN 1 THEN r.created_at + interval '30 minutes' ELSE r.updated_at - interval '15 minutes' END AS assessed_at
    FROM risk.risk r CROSS JOIN (VALUES (1), (2)) AS v (version_no)
    WHERE r.id::text LIKE '00000000-10a7-%') AS a
CROSS JOIN LATERAL (SELECT CASE WHEN a.version_no = 2 AND a.n % 10 = 0 THEN 4
                                ELSE (hashint4(a.n * 31 + 6 + a.version_no) & 1048575) % 4 END AS cell) AS x
CROSS JOIN LATERAL (SELECT (ARRAY[1, 2, 2, 3, 4])[x.cell + 1] AS probability, (ARRAY[2, 2, 3, 3, 4])[x.cell + 1] AS impact,
                           (ARRAY['LOW', 'LOW', 'MEDIUM', 'MEDIUM', 'HIGH'])[x.cell + 1] AS rating) AS c;

INSERT INTO risk.risk_assessment_impact (id, risk_assessment_version_id, impact_dimension_item_id, impact_level, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10a9-4000-8000-' || right(v.id::text, 12))::uuid, v.id,
       (SELECT i.id FROM master_data_config.master_data_item i JOIN master_data_config.master_data_catalogue c ON c.id = i.catalogue_id
        WHERE c.code = 'IMPACT_DIMENSION' AND i.code = (ARRAY['COST', 'SCHEDULE', 'OPERATIONAL', 'REPUTATION'])[right(v.id::text, 12)::integer % 4 + 1]),
       v.overall_impact_level, v.created_at, v.created_by, v.created_at, v.created_by
FROM risk.risk_assessment_version v WHERE v.id::text LIKE '00000000-10a8-%';

INSERT INTO management_concern.management_concern (id, project_id, concern_type, category_item_id, priority_item_id, status, revision_no, raised_by_user_id, raised_at,
                                                   assignee_user_id, target_resolution_date, next_review_date, last_reviewed_at, resolved_at, closed_at,
                                                   description_lang, description, resolution_lang, resolution, title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10aa-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, pr.id,
       CASE WHEN (hashint4(c.n * 31 + 1) & 1048575) % 4 = 0 THEN 'CHALLENGE' ELSE 'ISSUE' END,
       '00000000-e2e9-4000-8000-000000000051',
       CASE WHEN (hashint4(c.n * 31 + 2) & 1048575) % 3 = 0 THEN '00000000-e2e9-4000-8000-000000000061'::uuid ELSE '00000000-e2e9-4000-8000-000000000062'::uuid END,
       s.status, 1, pr.project_manager_user_id, t.created_at,
       CASE WHEN s.status <> 'OPEN' THEN pr.project_manager_user_id END,
       (t.created_at + interval '45 days')::date,
       current_date - 20 + (hashint4(c.n * 31 + 3) & 1048575) % 60,
       CASE WHEN s.status <> 'OPEN' THEN t.updated_at END,
       CASE WHEN s.status IN ('RESOLVED', 'CLOSED') THEN t.updated_at - interval '2 hours' END,
       CASE WHEN s.status = 'CLOSED' THEN t.updated_at END,
       pr.title_lang, CASE WHEN pr.title_lang = 'ar' THEN 'وصف المسألة ' || c.k || ' في المشروع.' ELSE 'Description of item ' || c.k || ' on the project.' END,
       CASE WHEN s.status IN ('PENDING_VALIDATION', 'RESOLVED', 'CLOSED') THEN 'en' END,
       CASE WHEN s.status IN ('PENDING_VALIDATION', 'RESOLVED', 'CLOSED') THEN 'Resolved with the contractor; evidence attached.' END,
       pr.title_lang, CASE WHEN pr.title_lang = 'ar' THEN 'مسألة الحمل ' || c.n ELSE 'Load concern ' || c.n END,
       t.created_at, pr.project_manager_user_id, t.updated_at, pr.project_manager_user_id
FROM generate_series(1, 3000) AS p
JOIN project.project pr ON pr.id = ('00000000-10a6-4000-8000-' || lpad(p::text, 12, '0'))::uuid
CROSS JOIN LATERAL (SELECT k, (p - 1) * 8 + k AS n, (p - 1) * 10 + k + 4 AS approval_no FROM generate_series(1, 8) AS k) AS c
CROSS JOIN LATERAL (SELECT CASE WHEN c.approval_no % 50 = 0 THEN 'PENDING'
                                WHEN (hashint4(c.approval_no * 31 + 1) & 1048575) % 20 = 0 THEN 'REJECTED'
                                WHEN (hashint4(c.approval_no * 31 + 1) & 1048575) % 20 = 1 THEN 'RETURNED'
                                ELSE 'APPROVED' END AS outcome) AS o
CROSS JOIN LATERAL (SELECT CASE WHEN c.k <= 6 THEN
                                     CASE o.outcome WHEN 'PENDING' THEN 'PENDING_VALIDATION' WHEN 'APPROVED' THEN
                                          CASE WHEN (hashint4(c.n * 31 + 4) & 1048575) % 3 = 0 THEN 'RESOLVED' ELSE 'CLOSED' END
                                          ELSE 'IN_PROGRESS' END
                                WHEN p % 5 IN (1, 2) THEN 'IN_PROGRESS'
                                ELSE (ARRAY['OPEN', 'ASSIGNED', 'IN_PROGRESS'])[(hashint4(c.n * 31 + 5) & 1048575) % 3 + 1] END AS status) AS s
CROSS JOIN LATERAL (SELECT now() - ((c.n::bigint * 7919) % 24000) * interval '2628 seconds' AS updated_at) AS u
CROSS JOIN LATERAL (SELECT u.updated_at, u.updated_at - ((hashint4(c.n * 31 + 6) & 1048575) % 120) * interval '1 day' - interval '3 hours' AS created_at) AS t;

INSERT INTO management_concern.concern_escalation (id, management_concern_id, escalation_no, escalated_by_user_id, escalated_at, escalated_to_role_id, status,
                                                   resolved_at, resolved_by_user_id, request_key, reason_lang, reason, resolution_lang, resolution,
                                                   created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10ab-4000-8000-' || lpad(e.n::text, 12, '0'))::uuid, e.concern_id, 1, e.manager_id, e.escalated_at, '00000000-0000-4000-8000-000000000003', e.status,
       CASE WHEN e.status <> 'OPEN' THEN e.escalated_at + interval '1 hour' END,
       CASE WHEN e.status = 'RESOLVED' THEN e.department_manager_id WHEN e.status = 'WITHDRAWN' THEN e.manager_id END,
       md5('load-escalation:' || e.n)::uuid, 'en', 'Blocked for more than two weeks; department decision needed.',
       CASE WHEN e.status = 'RESOLVED' THEN 'en' END, CASE WHEN e.status = 'RESOLVED' THEN 'The department agreed the workaround.' END,
       e.escalated_at, e.manager_id, CASE WHEN e.status = 'OPEN' THEN e.escalated_at ELSE e.escalated_at + interval '1 hour' END,
       CASE WHEN e.status = 'RESOLVED' THEN e.department_manager_id ELSE e.manager_id END
FROM (
    SELECT (p - 1) * 8 + k AS n, ('00000000-10aa-4000-8000-' || lpad(((p - 1) * 8 + k)::text, 12, '0'))::uuid AS concern_id,
           pr.project_manager_user_id AS manager_id,
           ('00000000-10a1-4000-8000-' || lpad((11 + 2 * ((p - 1) % 13))::text, 12, '0'))::uuid AS department_manager_id,
           CASE WHEN k = 8 AND p % 25 IN (1, 6, 11, 16) THEN 'OPEN'
                WHEN (hashint4(((p - 1) * 8 + k) * 31 + 7) & 1048575) % 3 = 0 THEN 'WITHDRAWN' ELSE 'RESOLVED' END AS status,
           mc.created_at + (mc.updated_at - mc.created_at) / 2 AS escalated_at
    FROM generate_series(1, 3000) AS p
    JOIN project.project pr ON pr.id = ('00000000-10a6-4000-8000-' || lpad(p::text, 12, '0'))::uuid
    CROSS JOIN (VALUES (7), (8)) AS kk (k)
    JOIN management_concern.management_concern mc ON mc.id = ('00000000-10aa-4000-8000-' || lpad(((p - 1) * 8 + k)::text, 12, '0'))::uuid
    WHERE p % 5 IN (1, 2)) AS e;

\endif
