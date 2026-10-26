-- 70-progress-financial.sql — WF-02 progress reporting and WF-14 financials and KPIs (TASK-086): what FG-01's dashboards
-- (PROGRESS.*, FINANCIAL_KPI.*) and FG-02's reports read for every project, at indexing-strategy.md §5.2's "monthly for two
-- years per project" (72,000 each) as far as the lifecycle allows.
--
-- Only activated projects (ACTIVE, SUSPENDED, COMPLETED, CLOSED: dashboards.md D-7's eligible population for every delivery
-- projection) report: a project before activation has no reporting period, figures or KPIs. That is 2,718 projects
-- (20-projects.sql), so the monthly tables hold 64,748 published rows (65,312 with the open revisions; 67,534 periods)
-- rather than §5.2's 72,000, which counted every project; KPIs 13,590 assignments and 163,080 measurements rather than
-- §5.2's 15,000 and 180,000.
--
-- progress.reporting_cycle            monthly periods (10b8-<(p-1)*25 + m>): m = 1..24 the 24 calendar months before the
--                                     load's, CLOSED; m = 25 the load's own month, OPEN and not yet due, for ACTIVE projects
--                                     only. Due 5 days after the period ends. One ACTIVE project in ten (p % 10 = 3) is
--                                     behind: its periods 23 and 24 are still OPEN and past due, so REPORTING_COMPLETENESS
--                                     shows it OVERDUE and its published values STALE (dashboards.md D-5): 242 projects
-- progress.progress_submission        one PUBLISHED revision per CLOSED period (10b9-<same n>), submitted by the Project
--                                     Manager and reviewed by a Portfolio Manager, against the project's baseline; plus a
--                                     SUBMITTED revision of the open period of every fourth ACTIVE project (p % 4 = 0)
-- progress.published_progress_snapshot one per PUBLISHED revision (10ba-<same n>): actual and planned percentages rising
--                                     about 4 points a month, Overall Project Health, Schedule Health and Financial Status by
--                                     hash (GREEN 60%, AMBER 25%, RED 12%, UNKNOWN 3%), under the e2e WORKFLOW_POLICY version
-- progress.project_health_status      the CURRENT/LIVE Overall Project Health of each activated project (10bb-p)
-- financial_kpi.financial_commitment  the ACTIVE Approved Budget of each activated project (10bd-p), its registration budget
-- financial_kpi.financial_progress_update
--                                     one PUBLISHED update per CLOSED period (10be-<same n>), and a SUBMITTED one beside each
--                                     SUBMITTED progress revision; one in thirty MISSING (no figures), the rest MEASURED, with
--                                     a forecast at completion 93% to 112% of the budget
-- financial_kpi.published_financial_snapshot
--                                     one per PUBLISHED update (10bf-<same n>), with the budget it was published against and
--                                     its Financial Status under the e2e thresholds (AMBER over 5%, RED over 10%; UNKNOWN when
--                                     MISSING)
-- master_data_config.kpi_definition   4 more PUBLISHED definitions (10c0-1..4) beside 47-kpi-definitions.sql's E2E_SAFE_HOURS,
--                                     so a project can hold §5.2's five
-- financial_kpi.kpi_assignment        5 ACTIVE assignments per activated project (10c1-<(p-1)*5 + j>), owned by its Project
--                                     Manager, monthly; §5.2: 15,000 for every project
-- financial_kpi.kpi_target_version    the ACTIVE target of each (10c2-<same>): 95, GREEN from 90, AMBER from 80
-- financial_kpi.kpi_measurement       12 PUBLISHED monthly measurements per assignment, the 12 months before the load's
--                                     (10c3-<(a-1)*12 + k>); one in twenty-five MISSING (UNKNOWN), the rest rated against
--                                     the target; §5.2: 180,000 for every project
--
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts (the FG-01 and FG-02
-- integration tests and e2e/stack/standins/fixtures/uj-009 write WF-02's publication exactly so); CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM progress.reporting_cycle WHERE id = '00000000-10b8-4000-8000-000000000001') AS load_progress_fresh \gset
\if :load_progress_fresh

SET LOCAL session_replication_role = replica;

-- The seed applies every file in one transaction, so a temporary table of an earlier file may still exist.
DROP TABLE IF EXISTS load_project;
CREATE TEMPORARY TABLE load_project ON COMMIT DROP AS
SELECT right(pr.id::text, 12)::integer AS p, pr.id, pr.lifecycle_state, pr.project_manager_user_id AS manager_id, pr.registration_budget_sar AS budget,
       pr.activated_at, ('00000000-10a1-4000-8000-' || lpad((1 + right(pr.id::text, 12)::integer % 10)::text, 12, '0'))::uuid AS reviewer_id,
       pr.lifecycle_state = 'ACTIVE' AND right(pr.id::text, 12)::integer % 10 = 3 AS behind
FROM project.project pr
WHERE pr.id::text LIKE '00000000-10a6-%' AND pr.lifecycle_state IN ('ACTIVE', 'SUSPENDED', 'COMPLETED', 'CLOSED');

-- The seed applies every file in one transaction, so a temporary table of an earlier file may still exist.
DROP TABLE IF EXISTS load_cycle;
CREATE TEMPORARY TABLE load_cycle ON COMMIT DROP AS
SELECT l.p, l.id AS project_id, l.manager_id, l.reviewer_id, l.budget, c.m, (l.p - 1) * 25 + c.m AS n, c.period_start, c.period_end,
       CASE WHEN c.m = 25 OR (l.behind AND c.m >= 23) THEN 'OPEN' ELSE 'CLOSED' END AS status,
       least(now() - interval '2 hours', c.period_end + interval '2 days 9 hours') AS submitted_at,
       least(now() - interval '1 hour', c.period_end + interval '4 days 11 hours') AS published_at,
       hashint4(((l.p - 1) * 25 + c.m) * 31 + 1) & 1048575 AS h
FROM load_project l
CROSS JOIN LATERAL (SELECT m, (date_trunc('month', current_date) - (25 - m) * interval '1 month')::date AS period_start,
                           (date_trunc('month', current_date) - (24 - m) * interval '1 month' - interval '1 day')::date AS period_end
                    FROM generate_series(1, 25) AS m) AS c
WHERE c.m <= 24 OR l.lifecycle_state = 'ACTIVE';

INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b8-4000-8000-' || lpad(n::text, 12, '0'))::uuid, project_id, period_start, period_end, period_end + 5, status,
       period_start, '00000000-0000-4000-8000-0000000000ff', CASE WHEN status = 'CLOSED' THEN published_at ELSE period_start::timestamptz END,
       CASE WHEN status = 'CLOSED' THEN reviewer_id ELSE '00000000-0000-4000-8000-0000000000ff'::uuid END
FROM load_cycle;

-- A PUBLISHED revision per CLOSED period; a SUBMITTED one for the open period of every fourth ACTIVE project.
INSERT INTO progress.progress_submission (id, project_id, reporting_cycle_id, revision_no, status, actual_percent_calculated, planned_percent, baseline_id,
                                          submitted_by_user_id, submitted_at, reviewed_by_user_id, reviewed_at, narrative_lang, narrative,
                                          created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10b9-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, c.project_id, ('00000000-10b8-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, 1,
       CASE WHEN c.status = 'CLOSED' THEN 'PUBLISHED' ELSE 'SUBMITTED' END,
       v.actual, v.planned, ('00000000-10b7-4000-8000-' || lpad(c.p::text, 12, '0'))::uuid,
       c.manager_id, CASE WHEN c.status = 'CLOSED' THEN c.submitted_at ELSE now() - interval '6 hours' END,
       CASE WHEN c.status = 'CLOSED' THEN c.reviewer_id END, CASE WHEN c.status = 'CLOSED' THEN c.published_at END,
       'en', 'Works progressing to plan; no change to the critical path.',
       c.period_end::timestamptz - interval '1 day', c.manager_id,
       CASE WHEN c.status = 'CLOSED' THEN c.published_at ELSE now() - interval '6 hours' END,
       CASE WHEN c.status = 'CLOSED' THEN c.reviewer_id ELSE c.manager_id END
FROM load_cycle c
CROSS JOIN LATERAL (SELECT least(100, greatest(0, c.m * 4 + c.h % 7 - 3))::numeric(18, 4) AS actual, least(100, c.m * 4.2)::numeric(18, 4) AS planned) AS v
WHERE c.status = 'CLOSED' OR (c.m = 25 AND c.p % 4 = 0);

INSERT INTO progress.published_progress_snapshot (id, project_id, reporting_cycle_id, progress_submission_id, published_at, published_by_user_id, actual_percent,
                                                  is_overridden, planned_percent, overall_health, schedule_health, financial_status,
                                                  health_rule_configuration_version_id, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10ba-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, c.project_id, s.reporting_cycle_id, s.id, c.published_at, c.reviewer_id,
       s.actual_percent_calculated, false, s.planned_percent,
       (ARRAY['GREEN', 'AMBER', 'RED', 'UNKNOWN'])[g.overall], (ARRAY['GREEN', 'AMBER', 'RED', 'UNKNOWN'])[g.schedule], (ARRAY['GREEN', 'AMBER', 'RED', 'UNKNOWN'])[g.financial],
       '00000000-e2e8-4000-8000-000000000102', c.published_at, c.reviewer_id, c.published_at, c.reviewer_id
FROM load_cycle c
JOIN progress.progress_submission s ON s.id = ('00000000-10b9-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid
CROSS JOIN LATERAL (SELECT CASE WHEN c.h % 100 < 60 THEN 1 WHEN c.h % 100 < 85 THEN 2 WHEN c.h % 100 < 97 THEN 3 ELSE 4 END AS overall,
                           CASE WHEN (c.h / 100) % 100 < 60 THEN 1 WHEN (c.h / 100) % 100 < 85 THEN 2 WHEN (c.h / 100) % 100 < 97 THEN 3 ELSE 4 END AS schedule,
                           CASE WHEN (c.h / 10000) % 100 < 60 THEN 1 WHEN (c.h / 10000) % 100 < 85 THEN 2 WHEN (c.h / 10000) % 100 < 97 THEN 3 ELSE 4 END AS financial) AS g
WHERE c.status = 'CLOSED';

-- WF-02's CURRENT/LIVE health, recomputed on every change: the latest published percentages, computed in the last three days.
INSERT INTO progress.project_health_status (id, project_id, overall_health, actual_percent, planned_percent, computed_at, health_rule_configuration_version_id,
                                            created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10bb-4000-8000-' || lpad(l.p::text, 12, '0'))::uuid, l.id,
       CASE WHEN h.v % 100 < 60 THEN 'GREEN' WHEN h.v % 100 < 85 THEN 'AMBER' WHEN h.v % 100 < 97 THEN 'RED' ELSE 'UNKNOWN' END,
       least(100, 92 + h.v % 9)::numeric(18, 4), 100, now() - (h.v % 72) * interval '1 hour', '00000000-e2e8-4000-8000-000000000102',
       l.activated_at, '00000000-0000-4000-8000-0000000000ff', now() - (h.v % 72) * interval '1 hour', '00000000-0000-4000-8000-0000000000ff'
FROM load_project l
CROSS JOIN LATERAL (SELECT hashint4(l.p * 31 + 12) & 1048575 AS v) AS h;

INSERT INTO financial_kpi.financial_commitment (id, project_id, commitment_type, version_no, revision_no, status, amount_sar, effective_from, activated_at, source_type,
                                                as_of_date, entered_by_user_id, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10bd-4000-8000-' || lpad(p::text, 12, '0'))::uuid, id, 'APPROVED_BUDGET', 1, 1, 'ACTIVE', budget, activated_at::date, activated_at, 'MANUAL',
       activated_at::date, manager_id, activated_at - interval '10 days', manager_id, activated_at, reviewer_id
FROM load_project;

INSERT INTO financial_kpi.financial_progress_update (id, project_id, reporting_cycle_id, revision_no, status, actual_expenditure_to_date_sar, forecast_at_completion_sar,
                                                     value_status, source_type, as_of_date, entered_by_user_id, submitted_by_user_id, submitted_at,
                                                     reviewed_by_user_id, reviewed_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10be-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, c.project_id, ('00000000-10b8-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, 1,
       CASE WHEN c.status = 'CLOSED' THEN 'PUBLISHED' ELSE 'SUBMITTED' END,
       f.actual, f.forecast, CASE WHEN f.actual IS NULL THEN 'MISSING' ELSE 'MEASURED' END, 'MANUAL', c.period_end,
       c.manager_id, c.manager_id, CASE WHEN c.status = 'CLOSED' THEN c.submitted_at ELSE now() - interval '6 hours' END,
       CASE WHEN c.status = 'CLOSED' THEN c.reviewer_id END, CASE WHEN c.status = 'CLOSED' THEN c.published_at END,
       c.period_end::timestamptz - interval '1 day', c.manager_id,
       CASE WHEN c.status = 'CLOSED' THEN c.published_at ELSE now() - interval '6 hours' END,
       CASE WHEN c.status = 'CLOSED' THEN c.reviewer_id ELSE c.manager_id END
FROM load_cycle c
CROSS JOIN LATERAL (SELECT CASE WHEN c.h % 30 <> 0 THEN round(c.budget * least(100, c.m * 4) / 100 * (90 + (c.h / 7) % 20) / 100, 2) END AS actual,
                           CASE WHEN c.h % 30 <> 0 THEN round(c.budget * (93 + (c.h / 11) % 20) / 100, 2) END AS forecast) AS f
WHERE c.status = 'CLOSED' OR (c.m = 25 AND c.p % 4 = 0);

INSERT INTO financial_kpi.published_financial_snapshot (id, project_id, reporting_cycle_id, financial_progress_update_id, financial_commitment_id, published_at,
                                                        published_by_user_id, approved_budget_sar, actual_expenditure_to_date_sar, forecast_at_completion_sar,
                                                        value_status, financial_status, threshold_configuration_version_id, source_type, as_of_date, entered_by_user_id,
                                                        created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10bf-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid, c.project_id, u.reporting_cycle_id, u.id, ('00000000-10bd-4000-8000-' || lpad(c.p::text, 12, '0'))::uuid,
       c.published_at, c.reviewer_id, c.budget, u.actual_expenditure_to_date_sar, u.forecast_at_completion_sar, u.value_status,
       CASE WHEN u.value_status <> 'MEASURED' THEN 'UNKNOWN'
            WHEN u.forecast_at_completion_sar > c.budget * 1.10 THEN 'RED'
            WHEN u.forecast_at_completion_sar > c.budget * 1.05 THEN 'AMBER'
            ELSE 'GREEN' END,
       '00000000-e2e8-4000-8000-000000000102', 'MANUAL', u.as_of_date, c.manager_id, c.published_at, c.reviewer_id, c.published_at, c.reviewer_id
FROM load_cycle c
JOIN financial_kpi.financial_progress_update u ON u.id = ('00000000-10be-4000-8000-' || lpad(c.n::text, 12, '0'))::uuid
WHERE c.status = 'CLOSED';

INSERT INTO master_data_config.kpi_definition (id, code, name_ar, name_en, description_ar, description_en, unit_item_id, direction, lifecycle_state,
                                               published_at, published_by_user_id, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c0-4000-8000-' || lpad(k.no::text, 12, '0'))::uuid, k.code, k.name_ar, k.name_en, k.description_ar, k.description_en,
       '00000000-e2e9-4000-8000-0000000000c1', 'HIGHER_IS_BETTER', 'PUBLISHED', now() - interval '800 days', '00000000-0000-4000-8000-0000000000ff',
       now() - interval '800 days', '00000000-0000-4000-8000-0000000000ff', now() - interval '800 days', '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES (1, 'LOAD_QUALITY_PASS_RATE', 'نسبة اجتياز الجودة', 'Quality pass rate', 'نسبة الفحوص المجتازة من المرة الأولى.', 'Share of inspections passed first time.'),
             (2, 'LOAD_LOCAL_CONTENT', 'المحتوى المحلي', 'Local content', 'نسبة الإنفاق على المحتوى المحلي.', 'Share of spend on local content.'),
             (3, 'LOAD_PERMIT_ON_TIME', 'التصاريح في موعدها', 'Permits on time', 'نسبة التصاريح الصادرة في موعدها.', 'Share of permits issued on time.'),
             (4, 'LOAD_STAKEHOLDER_SATISFACTION', 'رضا أصحاب المصلحة', 'Stakeholder satisfaction', 'نسبة الرضا في الاستبيان الشهري.', 'Satisfaction in the monthly survey.'))
    AS k (no, code, name_ar, name_en, description_ar, description_en);

INSERT INTO financial_kpi.kpi_assignment (id, project_id, kpi_definition_id, owner_user_id, measurement_frequency_item_id, status, assigned_at,
                                          created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c1-4000-8000-' || lpad(((l.p - 1) * 5 + j)::text, 12, '0'))::uuid, l.id,
       CASE WHEN j = 1 THEN '00000000-e2e9-4000-8000-000000000401'::uuid ELSE ('00000000-10c0-4000-8000-' || lpad((j - 1)::text, 12, '0'))::uuid END,
       l.manager_id, '00000000-e2e9-4000-8000-0000000000d1', 'ACTIVE', l.activated_at, l.activated_at, l.manager_id, l.activated_at, l.manager_id
FROM load_project l CROSS JOIN generate_series(1, 5) AS j;

INSERT INTO financial_kpi.kpi_target_version (id, kpi_assignment_id, version_no, revision_no, status, target_value, green_threshold, amber_threshold, effective_from,
                                              activated_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c2-4000-8000-' || right(a.id::text, 12))::uuid, a.id, 1, 1, 'ACTIVE', 95, 90, 80, a.assigned_at::date, a.assigned_at + interval '2 days',
       a.assigned_at, a.owner_user_id, a.assigned_at + interval '2 days', '00000000-10a1-4000-8000-000000000001'
FROM financial_kpi.kpi_assignment a WHERE a.id::text LIKE '00000000-10c1-%';

INSERT INTO financial_kpi.kpi_measurement (id, kpi_assignment_id, kpi_target_version_id, period_start, period_end, measured_value, value_status, rag_status, as_of_date,
                                           recorded_by_user_id, status, submitted_at, published_by_user_id, published_at, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c3-4000-8000-' || lpad(((x.a - 1) * 12 + x.k)::text, 12, '0'))::uuid, x.assignment_id, ('00000000-10c2-4000-8000-' || lpad(x.a::text, 12, '0'))::uuid,
       x.period_start, x.period_end, v.value, CASE WHEN v.value IS NULL THEN 'MISSING' ELSE 'MEASURED' END,
       CASE WHEN v.value IS NULL THEN 'UNKNOWN' WHEN v.value >= 90 THEN 'GREEN' WHEN v.value >= 80 THEN 'AMBER' ELSE 'RED' END,
       x.period_end, x.owner_id, 'PUBLISHED', x.at - interval '1 day', x.reviewer_id, x.at,
       x.at - interval '2 days', x.owner_id, x.at, x.reviewer_id
FROM (
    SELECT right(a.id::text, 12)::integer AS a, a.id AS assignment_id, a.owner_user_id AS owner_id, k,
           ('00000000-10a1-4000-8000-' || lpad((1 + right(a.project_id::text, 12)::integer % 10)::text, 12, '0'))::uuid AS reviewer_id,
           (date_trunc('month', current_date) - (13 - k) * interval '1 month')::date AS period_start,
           (date_trunc('month', current_date) - (12 - k) * interval '1 month' - interval '1 day')::date AS period_end,
           least(now() - interval '1 hour', date_trunc('month', current_date) - (12 - k) * interval '1 month' + interval '6 days') AS at,
           hashint4((right(a.id::text, 12)::integer * 12 + k) * 31 + 1) & 1048575 AS h
    FROM financial_kpi.kpi_assignment a CROSS JOIN generate_series(1, 12) AS k
    WHERE a.id::text LIKE '00000000-10c1-%') AS x
CROSS JOIN LATERAL (SELECT CASE WHEN x.h % 25 <> 0 THEN (78 + x.h % 23)::numeric(18, 4) END AS value) AS v;

\endif
