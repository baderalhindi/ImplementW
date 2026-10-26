-- 90-analyze.sql — refresh the planner's statistics after the volume load (TASK-086), as autovacuum would in service before
-- the first request arrives; indexing-strategy.md §5.2 ran VACUUM ANALYZE before its review for the same reason. Without it
-- the planner plans the load test's first minutes against the empty tables' statistics and chooses sequential scans.
-- ANALYZE runs inside the --single-transaction this folder is applied with; VACUUM cannot, and a freshly loaded table has
-- no dead rows for it to reclaim. Safe to re-run.

ANALYZE;
