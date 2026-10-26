-- uj-013-reconciliation-dead-letter — UJ-013 (technical recovery, FG-05).
--
-- Stands in for: a directory reconciliation that ran while the directory was unreachable. FG-05's engine made its three
-- permitted attempts (integration-monitoring.md D-6: 60 s back-off, doubling), each a technical failure (UNREACHABLE) with a
-- FAILED reconciliation run, and the third became the lineage's dead letter. On SIT, QA produces it by blocking the
-- directory while a reconciliation runs; the e2e stack's directory cannot be taken away from the running API, so the
-- attempts are written as the engine writes them (IntegrationMonitoring/IntegrationFailureSimulationTests). No alert is
-- written: the journey's subject is the replay, and an outage's alerts are FG-05's own tests'.
--
-- Parameters: lineageKey (unique per run), correlationId (a uuid). Prints the dead letter's invocation id.

INSERT INTO integration_monitoring.sync_run (id, integration_instance_id, run_type, correlation_id, started_at, completed_at, status,
                                             records_read, records_applied, records_rejected, mismatch_count,
                                             created_at, created_by, updated_at, updated_by)
SELECT md5('uj-013-run:' || :'lineageKey' || ':' || a.attempt_no)::uuid, n.id, 'RECONCILIATION', :'correlationId'::uuid,
       now() - make_interval(mins => a.minutes_ago), now() - make_interval(mins => a.minutes_ago) + interval '2 seconds', 'FAILED',
       0, 0, 0, 0,
       now() - make_interval(mins => a.minutes_ago), '00000000-0000-4000-8000-0000000000f3',
       now() - make_interval(mins => a.minutes_ago) + interval '2 seconds', '00000000-0000-4000-8000-0000000000f3'
FROM (VALUES (1, 30), (2, 29), (3, 27)) AS a (attempt_no, minutes_ago)
CROSS JOIN integration_monitoring.integration_instance n
JOIN integration_monitoring.integration_definition d ON d.id = n.integration_definition_id
WHERE d.code = 'DIRECTORY' AND n.code = 'PRIMARY';

-- Attempts 1 and 2 were followed by a retry (D-7: then plainly FAILED); attempt 3 is the dead letter.
INSERT INTO integration_monitoring.invocation (id, integration_instance_id, sync_run_id, operation_code, correlation_id, idempotency_key, attempt_no,
                                               source_module, source_reference, started_at, completed_at, status, failure_category,
                                               response_code, error_summary, next_attempt_at, created_at, created_by, updated_at, updated_by)
SELECT md5('uj-013-attempt:' || :'lineageKey' || ':' || a.attempt_no)::uuid, n.id, md5('uj-013-run:' || :'lineageKey' || ':' || a.attempt_no)::uuid,
       'RECONCILE', :'correlationId'::uuid, 'uj-013:' || :'lineageKey', a.attempt_no, 'IntegrationMonitoring', NULL,
       now() - make_interval(mins => a.minutes_ago), now() - make_interval(mins => a.minutes_ago) + interval '2 seconds', a.status, 'TECHNICAL',
       'UNREACHABLE', 'SocketException', NULL,
       now() - make_interval(mins => a.minutes_ago), '00000000-0000-4000-8000-0000000000f3',
       now() - make_interval(mins => a.minutes_ago) + interval '2 seconds', '00000000-0000-4000-8000-0000000000f3'
FROM (VALUES (1, 30, 'FAILED'), (2, 29, 'FAILED'), (3, 27, 'DEAD_LETTER')) AS a (attempt_no, minutes_ago, status)
CROSS JOIN integration_monitoring.integration_instance n
JOIN integration_monitoring.integration_definition d ON d.id = n.integration_definition_id
WHERE d.code = 'DIRECTORY' AND n.code = 'PRIMARY';

SELECT md5('uj-013-attempt:' || :'lineageKey' || ':3')::uuid;
