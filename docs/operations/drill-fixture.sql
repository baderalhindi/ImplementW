-- drill-fixture.sql — data added to a local source database before a restore rehearsal or drill (TASK-023).
--
-- Used by rehearse-restore.sh and run-restore-drill.sh, on top of the TASK-014 schema and seed. Never
-- run against a real environment: it creates a stand-in audit_activity schema that TASK-073 owns.

-- A stand-in for TASK-073's table, reduced to the columns the chain check reads.
CREATE SCHEMA audit_activity;
CREATE TABLE audit_activity.audit_event (
    id                  uuid PRIMARY KEY,
    recorded_at         timestamptz NOT NULL,
    event_class         varchar(50) NOT NULL,
    previous_event_hash char(64),
    event_hash          char(64) NOT NULL UNIQUE
);
DO $$
DECLARE
    previous char(64);
    current  char(64);
BEGIN
    FOR i IN 1..1000 LOOP
        current := encode(sha256(convert_to(coalesce(previous, '') || i::text, 'UTF8')), 'hex');
        INSERT INTO audit_activity.audit_event
        VALUES (md5('event' || i)::uuid, timestamptz '2026-09-25 00:00:00+00' + i * interval '1 second',
                'DATA_CHANGE', previous, current);
        previous := current;
    END LOOP;
END $$;

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
