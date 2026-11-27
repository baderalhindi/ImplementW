-- drill-fixture.sql — data added to a local source database before a restore rehearsal or drill (TASK-023).
--
-- Used by rehearse-restore.sh and run-restore-drill.sh, on top of the migrated schema and the local seed. Never
-- run against a real environment: it writes synthetic events into the real audit store.
--
-- It creates no table a migration owns (QA PHASE-E-03: the stand-in audit_activity.audit_event it used to create
-- collided with TASK-033's table once the migrations created it). The audit events go into the migrated
-- audit_activity.audit_event, and the table's own trigger (chain_audit_event, TASK-033) sets recorded_at,
-- previous_event_hash and event_hash, so the chain the restore must preserve is the product's, not a copy of it.

-- 1,000 chained events, one statement: each row's trigger sees the rows before it. Their ids are md5('event' || i),
-- so a rehearsal can name one (rehearse-restore.sh F6, F9).
INSERT INTO audit_activity.audit_event
    (id, occurred_at, event_class, event_type, actor_type, correlation_id, outcome,
     created_at, created_by, updated_at, updated_by)
SELECT md5('event' || i)::uuid,
       timestamptz '2026-09-25 00:00:00+00' + i * interval '1 second',
       'CONFIGURATION_CHANGE', 'DRILL_FIXTURE', 'SERVICE', md5('correlation' || i)::uuid, 'SUCCESS',
       timestamptz '2026-09-25 00:00:00+00' + i * interval '1 second', md5('drill-fixture')::uuid,
       timestamptz '2026-09-25 00:00:00+00' + i * interval '1 second', md5('drill-fixture')::uuid
FROM generate_series(1, 1000) AS i
ORDER BY i;

-- Volume, a sequence and an index: what a restore can silently get wrong beyond row content.
CREATE SCHEMA drill_fixture;
CREATE SEQUENCE drill_fixture.load_id_seq;
CREATE TABLE drill_fixture.load (
    id          bigint PRIMARY KEY DEFAULT nextval('drill_fixture.load_id_seq'),
    payload     text NOT NULL,
    recorded_at timestamptz NOT NULL
);
INSERT INTO drill_fixture.load (payload, recorded_at)
SELECT md5(g::text) || ' — نص عربي', timestamptz '2026-09-25 00:00:00+00' + g * interval '1 second'
FROM generate_series(1, 200000) AS g;
CREATE INDEX load_recorded_at ON drill_fixture.load (recorded_at);
