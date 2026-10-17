-- uj-009-published-progress-period — one WF-02 reporting period published, written as WF-02's publication stores it in one
-- transaction (progress-update.md D-6): the period CLOSED, its revision PUBLISHED (submitted by the project's Project Manager,
-- reviewed by AHDA's e2e Portfolio Manager), the Published Progress Snapshot, and the CURRENT/LIVE health recomputed with it.
-- Used by UJ-009 (Current, Published and Historical), UJ-010 (missing and stale) and UJ-015 (exports: WF-14 reports on a
-- WF-02 period, so the financial figures it publishes need one).
--
-- What it stands in for: WF-02 publication itself. In every environment an update answers 422 PROGRESS_ROLLUP_UNAVAILABLE,
-- because WF-02 has no source for task progress and the baseline yet (progress-update.md F-1, NoProgressInputs), so no period
-- can be published through the product. FG-01 and FG-02 read what WF-02 stored and nothing else; this writes exactly that,
-- with WF-02's guards off as the FG-01/FG-02 integration tests write it (DashboardDriver, ReportDriver). SIT must publish real
-- periods once F-1 is closed. The health and percentages are the caller's, under the e2e WORKFLOW_POLICY version.
--
-- Parameters: projectId; periodStartDaysAgo (the period is seven days from then); publishedDaysAgo; actualPercent;
-- plannedPercent; overallHealth and scheduleHealth (GREEN, AMBER, RED). Prints the snapshot's id.

SET LOCAL session_replication_role = replica;

SELECT gen_random_uuid() AS cycle_id, gen_random_uuid() AS submission_id, gen_random_uuid() AS snapshot_id,
       (current_date - :'periodStartDaysAgo'::integer) AS period_start,
       now() - make_interval(days => :'publishedDaysAgo'::integer) AS published_at,
       (SELECT project_manager_user_id FROM project.project WHERE id = :'projectId') AS manager_id,
       (SELECT id FROM schedule.project_baseline WHERE project_id = :'projectId' AND status = 'ACTIVE') AS baseline_id \gset

INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
VALUES (:'cycle_id', :'projectId', :'period_start', :'period_start'::date + 6, :'period_start'::date + 6, 'CLOSED',
        :'published_at', :'manager_id', :'published_at', '00000000-e2e1-4000-8000-000000000002');

INSERT INTO progress.progress_submission (id, project_id, reporting_cycle_id, revision_no, status, actual_percent_calculated, planned_percent, baseline_id,
                                          submitted_by_user_id, submitted_at, reviewed_by_user_id, reviewed_at, created_at, created_by, updated_at, updated_by)
VALUES (:'submission_id', :'projectId', :'cycle_id', 1, 'PUBLISHED', :'actualPercent', :'plannedPercent', :'baseline_id',
        :'manager_id', :'published_at'::timestamptz - interval '1 day', '00000000-e2e1-4000-8000-000000000002', :'published_at',
        :'published_at'::timestamptz - interval '2 days', :'manager_id', :'published_at', '00000000-e2e1-4000-8000-000000000002');

INSERT INTO progress.published_progress_snapshot (id, project_id, reporting_cycle_id, progress_submission_id, published_at, published_by_user_id, actual_percent,
                                                  is_overridden, planned_percent, overall_health, schedule_health, financial_status,
                                                  health_rule_configuration_version_id, created_at, created_by, updated_at, updated_by)
VALUES (:'snapshot_id', :'projectId', :'cycle_id', :'submission_id', :'published_at', '00000000-e2e1-4000-8000-000000000002', :'actualPercent',
        false, :'plannedPercent', :'overallHealth', :'scheduleHealth', 'GREEN', '00000000-e2e8-4000-8000-000000000102',
        :'published_at', '00000000-e2e1-4000-8000-000000000002', :'published_at', '00000000-e2e1-4000-8000-000000000002');

INSERT INTO progress.project_health_status (id, project_id, overall_health, actual_percent, planned_percent, computed_at, health_rule_configuration_version_id,
                                            created_at, created_by, updated_at, updated_by)
VALUES (gen_random_uuid(), :'projectId', :'overallHealth', :'actualPercent', :'plannedPercent', :'published_at', '00000000-e2e8-4000-8000-000000000102',
        :'published_at', '00000000-e2e1-4000-8000-000000000002', :'published_at', '00000000-e2e1-4000-8000-000000000002')
ON CONFLICT (project_id) DO UPDATE
SET overall_health = EXCLUDED.overall_health, actual_percent = EXCLUDED.actual_percent, planned_percent = EXCLUDED.planned_percent,
    computed_at = EXCLUDED.computed_at, updated_at = EXCLUDED.updated_at, updated_by = EXCLUDED.updated_by;

SELECT :'snapshot_id';
