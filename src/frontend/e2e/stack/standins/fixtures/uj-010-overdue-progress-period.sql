-- uj-010-overdue-progress-period — the passage of time on a project's progress reporting, for UJ-010 (missing and stale): the
-- WF-02 reporting period after the last published one, still OPEN, its due date some days behind today. WF-02 keeps a period
-- OPEN until its progress is published, so once one falls due unpublished the project's published values are older than the
-- obligation and FG-01/FG-02 must call them STALE (dashboards.md D-5, ReportingStaleness).
--
-- What it stands in for: the days passing between one publication and the next obligation, which a journey cannot wait for,
-- and WF-02's generation of the period, which only happens when an update starts and is blocked in every environment
-- (progress-update.md F-1). It writes the period as WF-02 stores one, with WF-02's guards off, as the FG-01/FG-02 integration
-- tests do (DashboardDriver.ReportingPeriodAsync). Nothing else changes: the published snapshot keeps its value and its as-of.
--
-- Parameters: projectId; periodStartDaysAgo (the period is seven days from then and due at its end). Prints the period's id.

SET LOCAL session_replication_role = replica;

SELECT gen_random_uuid() AS cycle_id, (current_date - :'periodStartDaysAgo'::integer) AS period_start,
       (SELECT project_manager_user_id FROM project.project WHERE id = :'projectId') AS manager_id \gset

INSERT INTO progress.reporting_cycle (id, project_id, period_start, period_end, due_date, status, created_at, created_by, updated_at, updated_by)
VALUES (:'cycle_id', :'projectId', :'period_start', :'period_start'::date + 6, :'period_start'::date + 6, 'OPEN',
        now(), :'manager_id', now(), :'manager_id');

SELECT :'cycle_id';
