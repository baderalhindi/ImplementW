-- 40-change-approval.sql — WF-08 change requests and the approval engine's instances and tasks at indexing-strategy.md §5.2's
-- volume (TASK-086): SCR-105 Change Requests, SCR-100 Approval Inbox, SCR-101 My Requests, SCR-115 Workflow History, and
-- FG-02's CHANGE_REQUEST.CHANGE_POSITION read these rows.
--
-- change_request.change_request   9,000  3 per project (c = 1..3, n = (p-1)*3 + c, id 10ae-n), raised by the project's Project
--                                        Manager; SCOPE, COST or SCHEDULE by hash, with the matching impact. Its status follows
--                                        its CHANGE_REQUEST approval: APPROVED → APPROVED, IMPLEMENTATION, IMPLEMENTED or CLOSED
--                                        by hash; REJECTED → REJECTED; RETURNED → RETURNED
-- approval.approval_instance     30,000  10 per project (i = 1..30000, project p = (i-1)/10 + 1, slot s = (i-1) % 10, id 10ac-i),
--                                        every one about a real subject of its project:
--                                          s = 0     PROJECT_REGISTRATION of the project
--                                          s = 1..3  CHANGE_REQUEST of change request s
--                                          s = 4..9  MANAGEMENT_CONCERN_RESOLUTION of concern s - 3 (30-risks-concerns.sql)
--                                        against the e2e APPROVAL_AUTHORITY version (40-configuration.sql)
-- approval.approval_task         60,000  2 stages per instance (10ad-<2i-1>, 10ad-<2i>): stage 1 R02, decided by a Portfolio
--                                        Manager (load.u001..u010); stage 2 R03, by the project department's Department Manager
--
-- Outcome of instance i (the rule 30-risks-concerns.sql repeats): PENDING when i % 50 = 0 (600: always a concern resolution,
-- whose concern is PENDING_VALIDATION); otherwise REJECTED when hashint4(i * 31 + 1) & 1048575 is 0 mod 20, RETURNED when
-- 1 mod 20, else APPROVED. A registration follows its project instead: APPROVED from APPROVED_PLANNED on, RETURNED for a
-- RETURNED project, WITHDRAWN for a DRAFT one (submitted, then withdrawn), and PENDING for a SUBMITTED or UNDER_REVIEW one.
--
-- A PENDING instance has stage 1 APPROVED and stage 2 PENDING. Of the 600 concern resolutions, by q = i / 50: q % 4 = 0 an R02
-- role queue (no user), 1 a personal task of Portfolio Manager load.u(001 + (q/4) % 10), 2 an R03 role queue, 3 a personal
-- task of one of the department's two Department Managers; so each Portfolio Manager has 15 personal tasks waiting and each
-- Department Manager about 6. A pending registration waits in the R03 role queue.
--
-- Deviation from §5.2 ("one task in a hundred PENDING, half of those in a role queue"): the 52 or so registrations of
-- SUBMITTED and UNDER_REVIEW projects (52) are PENDING as well, because a project under review is waiting on its approval; so
-- 652 tasks are PENDING (1.09%), 352 of them in a role queue. approval_delegation (§5.2: 300) is not loaded: the inbox reads it
-- only for a delegate, and no load user is one.
--
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts; CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM approval.approval_instance WHERE id = '00000000-10ac-4000-8000-000000000001') AS load_approvals_fresh \gset
\if :load_approvals_fresh

SET LOCAL session_replication_role = replica;

-- The seed applies every file in one transaction, so a temporary table of an earlier file may still exist.
DROP TABLE IF EXISTS load_approval_plan;
CREATE TEMPORARY TABLE load_approval_plan ON COMMIT DROP AS
SELECT a.i, a.p, a.s, pr.id AS project_id, pr.department_id, pr.project_manager_user_id AS manager_id, (pr.p_dept) AS dept,
       CASE WHEN a.s = 0 THEN
                CASE WHEN pr.lifecycle_state IN ('SUBMITTED', 'UNDER_REVIEW') THEN 'PENDING' WHEN pr.lifecycle_state = 'RETURNED' THEN 'RETURNED'
                     WHEN pr.lifecycle_state = 'DRAFT' THEN 'WITHDRAWN' ELSE 'APPROVED' END
            WHEN a.i % 50 = 0 THEN 'PENDING'
            WHEN (hashint4(a.i * 31 + 1) & 1048575) % 20 = 0 THEN 'REJECTED'
            WHEN (hashint4(a.i * 31 + 1) & 1048575) % 20 = 1 THEN 'RETURNED'
            ELSE 'APPROVED' END AS outcome,
       CASE WHEN a.s = 0 THEN 'PROJECT_REGISTRATION' WHEN a.s <= 3 THEN 'CHANGE_REQUEST' ELSE 'MANAGEMENT_CONCERN_RESOLUTION' END AS routing_key,
       CASE WHEN a.s = 0 THEN 'Project' WHEN a.s <= 3 THEN 'ChangeRequest' ELSE 'ManagementConcern' END AS subject_module,
       CASE WHEN a.s = 0 THEN pr.id
            WHEN a.s <= 3 THEN ('00000000-10ae-4000-8000-' || lpad(((a.p - 1) * 3 + a.s)::text, 12, '0'))::uuid
            ELSE ('00000000-10aa-4000-8000-' || lpad(((a.p - 1) * 8 + a.s - 3)::text, 12, '0'))::uuid END AS subject_id
FROM (SELECT i, (i - 1) / 10 + 1 AS p, (i - 1) % 10 AS s FROM generate_series(1, 30000) AS i) AS a
JOIN (SELECT id, department_id, project_manager_user_id, lifecycle_state, right(department_id::text, 12)::integer AS p_dept FROM project.project) AS pr
  ON pr.id = ('00000000-10a6-4000-8000-' || lpad(a.p::text, 12, '0'))::uuid;

ALTER TABLE load_approval_plan ADD COLUMN requested_at timestamptz;
UPDATE load_approval_plan
SET requested_at = CASE WHEN outcome = 'PENDING' THEN now() - (i % 7) * interval '1 day' - (i % 24) * interval '1 hour' - interval '2 hours'
                        ELSE now() - interval '3 days' - ((i::bigint * 7919) % 30000) * interval '2102 seconds' END;

INSERT INTO change_request.change_request (id, project_id, change_type, status, revision_no, requested_by_user_id, submitted_at, cost_impact_sar, schedule_impact_days,
                                           is_contractual_obligation, implemented_at, closed_at, justification_lang, justification, scope_impact_lang, scope_impact,
                                           title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10ae-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, l.project_id, c.change_type, c.status, 1, l.manager_id, l.requested_at,
       CASE WHEN c.change_type = 'COST' THEN (50000 + (hashint4(c.n * 31 + 2) & 1048575) % 400 * 1000)::numeric(18, 2) END,
       CASE WHEN c.change_type = 'SCHEDULE' THEN 5 + (hashint4(c.n * 31 + 3) & 1048575) % 60 END,
       false,
       CASE WHEN c.status IN ('IMPLEMENTED', 'CLOSED') THEN l.requested_at + interval '20 days' END,
       CASE WHEN c.status = 'CLOSED' THEN l.requested_at + interval '25 days' END,
       'en', 'Required by the revised site conditions.',
       CASE WHEN c.change_type = 'SCOPE' THEN 'en' END, CASE WHEN c.change_type = 'SCOPE' THEN 'Adds the access road to the scope.' END,
       CASE WHEN l.p % 2 = 1 THEN 'ar' ELSE 'en' END,
       CASE WHEN l.p % 2 = 1 THEN 'طلب تغيير الحمل ' || c.n ELSE 'Load change request ' || c.n END,
       l.requested_at - interval '2 days', l.manager_id,
       CASE WHEN c.status = 'CLOSED' THEN l.requested_at + interval '25 days' WHEN c.status = 'IMPLEMENTED' THEN l.requested_at + interval '20 days'
            ELSE l.requested_at + interval '2 days' END,
       l.manager_id
FROM load_approval_plan l
CROSS JOIN LATERAL (SELECT (l.p - 1) * 3 + l.s AS n) AS nn
CROSS JOIN LATERAL (SELECT nn.n, (ARRAY['SCOPE', 'COST', 'SCHEDULE'])[(hashint4(nn.n * 31 + 1) & 1048575) % 3 + 1] AS change_type,
                           CASE l.outcome WHEN 'REJECTED' THEN 'REJECTED' WHEN 'RETURNED' THEN 'RETURNED'
                                ELSE (ARRAY['APPROVED', 'IMPLEMENTATION', 'IMPLEMENTED', 'CLOSED'])[(hashint4(nn.n * 31 + 4) & 1048575) % 4 + 1] END AS status) AS c
WHERE l.s BETWEEN 1 AND 3;

INSERT INTO approval.approval_instance (id, subject_module, subject_type, subject_id, subject_revision_no, routing_key, authority_configuration_version_id,
                                        scope_project_id, scope_department_id, requested_by_user_id, requested_at, status, completed_at, outcome_idempotency_key,
                                        outcome_delivered_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10ac-4000-8000-' || lpad(l.i::text, 12, '0'))::uuid, l.subject_module, l.subject_module, l.subject_id, 1, l.routing_key,
       '00000000-e2e8-4000-8000-000000000103', l.project_id, l.department_id, l.manager_id, l.requested_at, l.outcome,
       CASE WHEN l.outcome <> 'PENDING' THEN l.requested_at + interval '2 days' END,
       'apr-00000000-10ac-4000-8000-' || lpad(l.i::text, 12, '0') || '-outcome',
       CASE WHEN l.outcome <> 'PENDING' THEN l.requested_at + interval '2 days' END,
       l.requested_at, l.manager_id, CASE WHEN l.outcome <> 'PENDING' THEN l.requested_at + interval '2 days' ELSE l.requested_at + interval '1 day' END,
       CASE WHEN l.outcome <> 'PENDING' THEN '00000000-0000-4000-8000-0000000000ff'::uuid ELSE ('00000000-10a1-4000-8000-' || lpad((1 + l.i % 10)::text, 12, '0'))::uuid END
FROM load_approval_plan l;

INSERT INTO approval.approval_task (id, approval_instance_id, sequence_no, assigned_role_id, assigned_user_id, acting_user_id, status, due_at, decided_at,
                                    decision_reason_lang, decision_reason, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10ad-4000-8000-' || lpad((2 * t.i - 2 + t.stage)::text, 12, '0'))::uuid, ('00000000-10ac-4000-8000-' || lpad(t.i::text, 12, '0'))::uuid,
       t.stage, ('00000000-0000-4000-8000-' || lpad(t.role_no::text, 12, '0'))::uuid, t.user_id,
       CASE WHEN t.status IN ('APPROVED', 'REJECTED', 'RETURNED') THEN t.user_id END,
       t.status, t.created_at + interval '3 days',
       CASE WHEN t.status IN ('APPROVED', 'REJECTED', 'RETURNED') THEN t.created_at + t.decision_after END,
       CASE WHEN t.status IN ('REJECTED', 'RETURNED') THEN 'en' END,
       CASE WHEN t.status = 'REJECTED' THEN 'Not justified against the approved baseline.' WHEN t.status = 'RETURNED' THEN 'Attach the revised cost estimate.' END,
       t.created_at, '00000000-0000-4000-8000-0000000000ff',
       CASE WHEN t.status = 'PENDING' THEN t.created_at ELSE t.created_at + t.decision_after END,
       CASE WHEN t.status = 'PENDING' OR t.user_id IS NULL THEN '00000000-0000-4000-8000-0000000000ff'::uuid ELSE t.user_id END
FROM (
    -- Stage 1: R02, decided (APPROVED) by a Portfolio Manager.
    SELECT l.i, 1 AS stage, 2 AS role_no, ('00000000-10a1-4000-8000-' || lpad((1 + l.i % 10)::text, 12, '0'))::uuid AS user_id,
           'APPROVED' AS status, l.requested_at AS created_at, interval '50 minutes' AS decision_after
    FROM load_approval_plan l
    UNION ALL
    -- Stage 2: R03 by the department's Department Manager, carrying the instance's outcome; a PENDING one as the header says.
    SELECT l.i, 2,
           CASE WHEN l.outcome = 'PENDING' AND l.s <> 0 AND (l.i / 50) % 4 IN (0, 1) THEN 2 ELSE 3 END,
           CASE WHEN l.outcome = 'PENDING' AND l.s = 0 THEN NULL
                WHEN l.outcome = 'PENDING' AND (l.i / 50) % 4 IN (0, 2) THEN NULL
                WHEN l.outcome = 'PENDING' AND (l.i / 50) % 4 = 1 THEN ('00000000-10a1-4000-8000-' || lpad((1 + ((l.i / 50) / 4) % 10)::text, 12, '0'))::uuid
                ELSE ('00000000-10a1-4000-8000-' || lpad((11 + 2 * (l.dept - 1) + CASE WHEN l.outcome = 'PENDING' THEN ((l.i / 50) / 4) % 2 ELSE l.i % 2 END)::text,
                                                         12, '0'))::uuid END,
           CASE l.outcome WHEN 'WITHDRAWN' THEN 'CANCELLED' ELSE l.outcome END,
           l.requested_at + interval '1 hour', interval '1 day'
    FROM load_approval_plan l) AS t;

\endif
