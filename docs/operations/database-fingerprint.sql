-- database-fingerprint.sql — what a restored database is compared on (TASK-023, CTL-36).
--
--   psql "<connection>" -X -q -At -v ON_ERROR_STOP=1 -f docs/operations/database-fingerprint.sql
--
-- Prints one line per fact, in a fixed order, so two databases are equal exactly when their outputs
-- are byte-identical and `diff` names what differs:
--
--   table|<schema>.<table>|<row count>|<md5 of every row, sorted>
--   sequence|<schema>.<sequence>|<last_value>|<is_called>
--   objects|<schema>|constraints=<n>|indexes=<n>|functions=<n>|triggers=<n>
--   audit-chain|events=<n>|genesis=<n>|broken=<n>|forks=<n>        (or audit-chain|absent)
--
-- Every user table in every non-system schema is covered, so a table added by a later migration is
-- covered without editing this file. Rows are hashed as their text form, sorted under the C
-- collation so the order does not depend on the server's locale. Sequences are included because a
-- restore that leaves one behind the data it numbers fails on the first insert, not at verification.
--
-- The audit-chain line checks audit_activity.audit_event (CTL-24) for linkage only: every
-- previous_event_hash names an event that exists, exactly one event starts the chain, and no event
-- is followed twice. Recomputing each event_hash needs TASK-073's hash definition and is not done
-- here. Until TASK-073 creates the table the line reads "absent".
--
-- Read-only: the session is set read-only before anything else runs, so this is safe against PROD.

SET default_transaction_read_only = on;
SET statement_timeout = 0;

SELECT format(
         $q$SELECT 'table|' || %L || '|' || count(*) || '|' || md5(coalesce(string_agg(t::text, E'\n' ORDER BY t::text COLLATE "C"), '')) FROM %I.%I AS t$q$,
         n.nspname || '.' || c.relname, n.nspname, c.relname)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname NOT LIKE 'pg\_%'
ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"
\gexec

SELECT format(
         $q$SELECT 'sequence|' || %L || '|' || last_value || '|' || is_called FROM %I.%I$q$,
         n.nspname || '.' || c.relname, n.nspname, c.relname)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'S'
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname NOT LIKE 'pg\_%'
ORDER BY n.nspname COLLATE "C", c.relname COLLATE "C"
\gexec

SELECT 'objects|' || n.nspname
       || '|constraints=' || (SELECT count(*) FROM pg_constraint x WHERE x.connamespace = n.oid)
       || '|indexes='     || (SELECT count(*) FROM pg_class x WHERE x.relnamespace = n.oid AND x.relkind = 'i')
       || '|functions='   || (SELECT count(*) FROM pg_proc x WHERE x.pronamespace = n.oid)
       || '|triggers='    || (SELECT count(*) FROM pg_trigger x JOIN pg_class r ON r.oid = x.tgrelid
                              WHERE r.relnamespace = n.oid AND NOT x.tgisinternal)
FROM pg_namespace n
WHERE n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname NOT LIKE 'pg\_%'
ORDER BY n.nspname COLLATE "C";

SELECT to_regclass('audit_activity.audit_event') IS NOT NULL AS has_audit_event \gset
\if :has_audit_event
SELECT 'audit-chain'
       || '|events='  || count(*)
       || '|genesis=' || count(*) FILTER (WHERE e.previous_event_hash IS NULL)
       || '|broken='  || count(*) FILTER (WHERE e.previous_event_hash IS NOT NULL AND NOT EXISTS (
                           SELECT 1 FROM audit_activity.audit_event p WHERE p.event_hash = e.previous_event_hash))
       || '|forks='   || (SELECT count(*) FROM (
                           SELECT previous_event_hash FROM audit_activity.audit_event
                           WHERE previous_event_hash IS NOT NULL
                           GROUP BY previous_event_hash HAVING count(*) > 1) f)
FROM audit_activity.audit_event e;
\else
SELECT 'audit-chain|absent';
\endif
