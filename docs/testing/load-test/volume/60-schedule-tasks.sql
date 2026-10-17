-- 60-schedule-tasks.sql — WF-03 schedules, milestones and live Schedule Health, WF-04 tasks and WF-05 achievement claims at
-- indexing-strategy.md §5.2's volume (TASK-086): SCR-046/062 milestones, SCR-047/063–066 tasks, FG-01's
-- SCHEDULE.SCHEDULE_HEALTH_STATUS and FG-02's PROJECT_TASK.OPEN_TASKS and MILESTONE.OPEN_ACHIEVEMENT_CLAIMS read these rows.
--
-- schedule.project_schedule          3,000  one per project (10b3-p), the parent a milestone needs
-- schedule.project_baseline          2,718  one APPROVED, ACTIVE baseline (version 1, 10b7-p) per activated project, finishing on
--                                           the project's planned end; WF-02's planned percentages and WF-03's variance name it
-- schedule.project_milestone        24,000  8 per project (m = 1..8, n = (p-1)*8 + m, id 10b5-n), forecast evenly over the
--                                           project's planned span. An activated project's past milestones are ACHIEVED (one in
--                                           forty CANCELLED), its future ones PLANNED; a completed or closed project has no
--                                           PLANNED milestone (a future one is CANCELLED); every other project's are PLANNED
-- milestone.milestone_achievement  15,101  an ACCEPTED achievement (10b6-n) for every ACHIEVED milestone, and a SUBMITTED claim
--                                           on the next PLANNED milestone of every third ACTIVE project (p % 3 = 0), waiting
--                                           for review: 14,444 ACCEPTED and 657 SUBMITTED. §5.2 does not size them
-- project_task.project_task        150,000  50 per project (t = 1..50, n = (p-1)*50 + t, id 10b4-n), planned back to back over the
--                                           project's span, four in five assigned to the project's Project Manager (My Tasks:
--                                           1,000 for an internal Project Manager, 3,000 for an entity's). An activated project's
--                                           finished tasks are COMPLETED (one in eight still IN_PROGRESS, overdue), current ones
--                                           IN_PROGRESS, future ones NOT_STARTED; one in fifty BLOCKED, one in fifty CANCELLED;
--                                           a completed or closed project's are COMPLETED; a not yet activated project's
--                                           NOT_STARTED. Flat (no parent task) and not linked to schedule activities
-- schedule.schedule_health_status    2,718  live Schedule Health per activated project against its baseline, under the e2e
--                                           WORKFLOW_POLICY thresholds (5 and 20 days): GREEN 55%, AMBER 25%, RED 15%, UNKNOWN 5%
--
-- Not loaded: schedule_activity, schedule_dependency and the baseline_* copies (the Gantt and baseline views read a whole
-- schedule of one project; §5.2 does not size them and the load profile does not open them).
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts; CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM schedule.project_schedule WHERE id = '00000000-10b3-4000-8000-000000000001') AS load_schedule_fresh \gset
\if :load_schedule_fresh

SET LOCAL session_replication_role = replica;

-- The seed applies every file in one transaction, so a temporary table of an earlier file may still exist.
DROP TABLE IF EXISTS load_project;
CREATE TEMPORARY TABLE load_project ON COMMIT DROP AS
SELECT right(pr.id::text, 12)::integer AS p, pr.id, pr.lifecycle_state, pr.project_manager_user_id AS manager_id, pr.planned_start_date AS start_date,
       pr.planned_end_date AS end_date, pr.planned_end_date - pr.planned_start_date AS span, pr.activated_at, pr.created_at,
       pr.lifecycle_state IN ('ACTIVE', 'SUSPENDED', 'COMPLETED', 'CLOSED') AS activated, pr.lifecycle_state IN ('COMPLETED', 'CLOSED') AS finished
FROM project.project pr WHERE pr.id::text LIKE '00000000-10a6-%';

INSERT INTO schedule.project_schedule (id, project_id, calendar_item_id, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b3-4000-8000-' || lpad(p::text, 12, '0'))::uuid, id, NULL, created_at, manager_id, created_at, manager_id
FROM load_project;

INSERT INTO schedule.project_baseline (id, project_id, baseline_type, version_no, revision_no, status, activated_at, baseline_finish_date,
                                       created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b7-4000-8000-' || lpad(p::text, 12, '0'))::uuid, id, 'APPROVED', 1, 1, 'ACTIVE', activated_at - interval '1 day', end_date,
       activated_at - interval '20 days', manager_id, activated_at - interval '1 day', '00000000-10a1-4000-8000-000000000001'
FROM load_project WHERE activated;

INSERT INTO schedule.project_milestone (id, project_id, project_schedule_id, milestone_category_item_id, forecast_date, status, sort_order, title_lang, title,
                                        created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b5-4000-8000-' || lpad(x.n::text, 12, '0'))::uuid, l.id, ('00000000-10b3-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid,
       '00000000-e2e9-4000-8000-000000000081', x.forecast_date,
       CASE WHEN NOT l.activated THEN 'PLANNED'
            WHEN x.forecast_date < current_date AND (hashint4(x.n * 31 + 1) & 1048575) % 40 <> 0 THEN 'ACHIEVED'
            WHEN x.forecast_date < current_date OR l.finished THEN 'CANCELLED'
            ELSE 'PLANNED' END,
       x.m, CASE WHEN l.p % 2 = 1 THEN 'ar' ELSE 'en' END,
       CASE WHEN l.p % 2 = 1 THEN 'معلم الحمل ' || x.m ELSE 'Load milestone ' || x.m END,
       l.created_at + interval '2 days', l.manager_id, least(now() - interval '1 hour', x.forecast_date + interval '4 days'), l.manager_id
FROM load_project l
CROSS JOIN LATERAL (SELECT m, (l.p - 1) * 8 + m AS n, l.start_date + (m * l.span) / 9 AS forecast_date FROM generate_series(1, 8) AS m) AS x;

-- ACCEPTED achievements of the ACHIEVED milestones, reviewed by a Portfolio Manager.
INSERT INTO milestone.milestone_achievement (id, project_milestone_id, project_id, revision_no, status, claimed_achievement_date, accepted_actual_achievement_date,
                                             submitted_by_user_id, submitted_at, reviewed_by_user_id, reviewed_at, narrative_lang, narrative,
                                             created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b6-4000-8000-' || right(m.id::text, 12))::uuid, m.id, m.project_id, 1, 'ACCEPTED', m.forecast_date, m.forecast_date,
       m.created_by, least(now() - interval '3 hours', m.forecast_date + interval '1 day'),
       ('00000000-10a1-4000-8000-' || lpad((1 + right(m.id::text, 12)::integer % 10)::text, 12, '0'))::uuid,
       least(now() - interval '2 hours', m.forecast_date + interval '3 days'), 'en', 'Achieved; completion certificate attached.',
       least(now() - interval '4 hours', m.forecast_date + interval '1 day'), m.created_by, least(now() - interval '2 hours', m.forecast_date + interval '3 days'),
       ('00000000-10a1-4000-8000-' || lpad((1 + right(m.id::text, 12)::integer % 10)::text, 12, '0'))::uuid
FROM schedule.project_milestone m
WHERE m.id::text LIKE '00000000-10b5-%' AND m.status = 'ACHIEVED';

-- One open claim (SUBMITTED) on the next PLANNED milestone of every third ACTIVE project.
INSERT INTO milestone.milestone_achievement (id, project_milestone_id, project_id, revision_no, status, claimed_achievement_date, submitted_by_user_id, submitted_at,
                                             narrative_lang, narrative, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b6-4000-8000-' || right(n.id::text, 12))::uuid, n.id, n.project_id, 1, 'SUBMITTED', current_date - 1, n.created_by, now() - interval '1 day',
       'en', 'Reached ahead of the forecast; evidence attached.', now() - interval '2 days', n.created_by, now() - interval '1 day', n.created_by
FROM (
    SELECT DISTINCT ON (m.project_id) m.id, m.project_id, m.created_by
    FROM schedule.project_milestone m JOIN load_project l ON l.id = m.project_id
    WHERE l.lifecycle_state = 'ACTIVE' AND l.p % 3 = 0 AND m.status = 'PLANNED'
    ORDER BY m.project_id, m.sort_order) AS n;

INSERT INTO project_task.project_task (id, project_id, assignee_user_id, priority_item_id, status, planned_start_date, planned_finish_date, planned_duration_days,
                                       actual_start_date, actual_finish_date, actual_percent_complete, completed_at, reopened_count, blocked_reason_lang, blocked_reason,
                                       title_lang, title, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b4-4000-8000-' || lpad(x.n::text, 12, '0'))::uuid, l.id,
       CASE WHEN x.t % 5 <> 0 THEN l.manager_id END,
       CASE WHEN x.t % 3 = 0 THEN '00000000-e2e9-4000-8000-000000000061'::uuid ELSE '00000000-e2e9-4000-8000-000000000062'::uuid END,
       s.status, x.planned_start, x.planned_finish, x.planned_finish - x.planned_start + 1,
       CASE WHEN s.status IN ('IN_PROGRESS', 'COMPLETED') THEN a.actual_start END,
       CASE WHEN s.status = 'COMPLETED' THEN a.actual_finish END,
       CASE s.status WHEN 'COMPLETED' THEN 100 WHEN 'IN_PROGRESS' THEN 5 + (hashint4(x.n * 31 + 3) & 1048575) % 90 END,
       CASE WHEN s.status = 'COMPLETED' THEN a.actual_finish + interval '17 hours' END,
       0,
       CASE WHEN s.status = 'BLOCKED' THEN 'en' END, CASE WHEN s.status = 'BLOCKED' THEN 'Waiting for the utility permit.' END,
       CASE WHEN l.p % 2 = 1 THEN 'ar' ELSE 'en' END,
       CASE WHEN l.p % 2 = 1 THEN 'مهمة الحمل ' || x.t ELSE 'Load task ' || x.t END,
       l.created_at + interval '3 days', l.manager_id,
       greatest(l.created_at + interval '3 days', least(now() - interval '1 hour', coalesce(a.actual_finish, x.planned_start)::timestamptz + interval '18 hours')),
       l.manager_id
FROM load_project l
CROSS JOIN LATERAL (SELECT t, (l.p - 1) * 50 + t AS n, l.start_date + ((t - 1) * l.span) / 50 AS planned_start,
                           l.start_date + ((t - 1) * l.span) / 50 + greatest(1, l.span / 50) - 1 + (hashint4(((l.p - 1) * 50 + t) * 31 + 1) & 1048575) % 5 AS planned_finish
                    FROM generate_series(1, 50) AS t) AS x
CROSS JOIN LATERAL (SELECT (hashint4(x.n * 31 + 2) & 1048575) % 50 AS h) AS hh
CROSS JOIN LATERAL (SELECT CASE WHEN NOT l.activated THEN 'NOT_STARTED'
                                WHEN hh.h = 0 THEN 'CANCELLED'
                                WHEN l.finished THEN 'COMPLETED'
                                WHEN hh.h = 1 THEN 'BLOCKED'
                                WHEN x.planned_finish < current_date THEN CASE WHEN hh.h % 8 = 2 THEN 'IN_PROGRESS' ELSE 'COMPLETED' END
                                WHEN x.planned_start <= current_date THEN 'IN_PROGRESS'
                                ELSE 'NOT_STARTED' END AS status) AS s
CROSS JOIN LATERAL (SELECT least(x.planned_start, current_date - 2) AS actual_start,
                           greatest(least(x.planned_start, current_date - 2), least(x.planned_finish, current_date - 1)) AS actual_finish) AS a;

INSERT INTO schedule.schedule_health_status (id, project_id, project_baseline_id, schedule_health, finish_variance_days, computed_at, health_rule_configuration_version_id,
                                             created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10bc-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid, l.id, ('00000000-10b7-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid, h.health,
       CASE h.health WHEN 'GREEN' THEN h.v % 5 WHEN 'AMBER' THEN 5 + h.v % 15 WHEN 'RED' THEN 20 + h.v % 40 END,
       now() - (h.v % 72) * interval '1 hour', '00000000-e2e8-4000-8000-000000000102',
       l.activated_at, '00000000-0000-4000-8000-0000000000ff', now() - (h.v % 72) * interval '1 hour', '00000000-0000-4000-8000-0000000000ff'
FROM load_project l
CROSS JOIN LATERAL (SELECT hashint4(l.p * 31 + 11) & 1048575 AS v) AS hv
CROSS JOIN LATERAL (SELECT hv.v, CASE WHEN hv.v % 100 < 55 THEN 'GREEN' WHEN hv.v % 100 < 80 THEN 'AMBER' WHEN hv.v % 100 < 95 THEN 'RED' ELSE 'UNKNOWN' END AS health) AS h
WHERE l.activated;

\endif
