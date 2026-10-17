-- 80-notifications.sql — WF-15 notification deliveries at indexing-strategy.md §5.2's volume (TASK-086): SCR-150 to SCR-153
-- and the unread badge every page load polls read these rows.
--
-- notifications.notification_delivery  400,000  for the 140 load users, about 2,857 each over the two years before the load
--                                                (§5.2: 2,667 for each of 150). Delivery n = 1..400000 (id 10c5-n) is created
--                                                at now() - (400000 - n) × 157 s. In each run of three, the first and third are
--                                                IN_APP and the second is the EMAIL copy of the first, so two in three are
--                                                IN_APP (266,667). The newest 1,000 IN_APP deliveries (n > 398,500) are unread
--                                                (SENT, about 7 per user); every older IN_APP one is READ an hour after it
--                                                arrived; every EMAIL one DELIVERED
-- notifications.notification_intent    266,667  the intents they deliver (10c4-<i + 1>, i = 0..266666): delivery n belongs to
--                                                intent 2 × ((n-1)/3), or 2 × ((n-1)/3) + 1 for the third of a run; intent i is
--                                                addressed to load.u(i % 140 + 1), COMPLETED
--
-- Every intent is the one source that publishes a NotificationIntent today, the WF-07 concern escalation (family
-- CONCERN_ESCALATION, 43-notification-routing.sql), about a real concern of the load set and its project, rendered with that
-- file's PUBLISHED IN_APP and EMAIL templates. The rendered subject and body are stored, as the router stores them, so
-- notification_intent_parameter (one row per template parameter, about a million rows) is not loaded: nothing on the load
-- profile reads it.
--
-- Guards off (session_replication_role = replica), as the repository's fixtures write stored facts; CHECKs hold, and
-- validate-data-integrity.sql checks the foreign keys afterwards. Dates relative to the load, as in 20-projects.sql.

SELECT NOT EXISTS (SELECT 1 FROM notifications.notification_intent WHERE id = '00000000-10c4-4000-8000-000000000001') AS load_notifications_fresh \gset
\if :load_notifications_fresh

SET LOCAL session_replication_role = replica;

INSERT INTO notifications.notification_intent (id, source_module, source_event_type, source_reference, event_family_code, subject_type, subject_id, scope_project_id,
                                               occurred_at, received_at, status, deep_link, created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c4-4000-8000-' || lpad((x.i + 1)::text, 12, '0'))::uuid, 'ManagementConcern', 'ManagementConcern.ConcernEscalated', 'load-escalation-' || (x.i + 1),
       'CONCERN_ESCALATION', 'ManagementConcern', ('00000000-10aa-4000-8000-' || lpad(x.concern_no::text, 12, '0'))::uuid,
       ('00000000-10a6-4000-8000-' || lpad(((x.concern_no - 1) / 8 + 1)::text, 12, '0'))::uuid,
       x.at - interval '1 minute', x.at - interval '50 seconds', 'COMPLETED',
       '/projects/00000000-10a6-4000-8000-' || lpad(((x.concern_no - 1) / 8 + 1)::text, 12, '0') || '/concerns/00000000-10aa-4000-8000-' || lpad(x.concern_no::text, 12, '0'),
       x.at - interval '50 seconds', '00000000-0000-4000-8000-0000000000ff', x.at + interval '5 seconds', '00000000-0000-4000-8000-0000000000ff'
FROM (
    SELECT i, (i::bigint * 7) % 24000 + 1 AS concern_no,
           now() - (400000 - (3 * (i / 2) + 1 + 2 * (i % 2))) * interval '157 seconds' AS at
    FROM generate_series(0, 266666) AS i) AS x;

INSERT INTO notifications.notification_delivery (id, notification_intent_id, recipient_user_id, channel, notification_template_id, rendered_language, rendered_subject,
                                                 rendered_body, status, attempt_count, last_attempt_at, sent_at, delivered_at, read_at,
                                                 created_at, created_by, updated_at, updated_by)
SELECT ('00000000-10c5-4000-8000-' || lpad(d.n::text, 12, '0'))::uuid, ('00000000-10c4-4000-8000-' || lpad((d.i + 1)::text, 12, '0'))::uuid,
       ('00000000-10a1-4000-8000-' || lpad((d.i % 140 + 1)::text, 12, '0'))::uuid, d.channel,
       CASE d.channel WHEN 'IN_APP' THEN '00000000-e2e8-4000-8000-000000000241'::uuid ELSE '00000000-e2e8-4000-8000-000000000242'::uuid END,
       'en', 'Escalation 1 of an issue or challenge',
       CASE d.channel WHEN 'IN_APP' THEN 'An item (ISSUE) on your project was escalated to role R03.'
            ELSE 'An item (ISSUE) on your project was escalated to role R03. Open it: /projects/00000000-10a6-4000-8000-'
                 || lpad(((d.concern_no - 1) / 8 + 1)::text, 12, '0') || '/concerns/00000000-10aa-4000-8000-' || lpad(d.concern_no::text, 12, '0') END,
       CASE WHEN d.channel = 'EMAIL' THEN 'DELIVERED' WHEN d.n > 398500 THEN 'SENT' ELSE 'READ' END,
       1, d.at + interval '2 seconds', d.at + interval '2 seconds', CASE WHEN d.channel = 'EMAIL' THEN d.at + interval '9 seconds' END,
       CASE WHEN d.channel = 'IN_APP' AND d.n <= 398500 THEN d.at + interval '1 hour' END,
       d.at, '00000000-0000-4000-8000-0000000000ff',
       CASE WHEN d.channel = 'IN_APP' AND d.n <= 398500 THEN d.at + interval '1 hour' WHEN d.channel = 'EMAIL' THEN d.at + interval '9 seconds' ELSE d.at + interval '2 seconds' END,
       CASE WHEN d.channel = 'IN_APP' AND d.n <= 398500 THEN ('00000000-10a1-4000-8000-' || lpad((d.i % 140 + 1)::text, 12, '0'))::uuid
            ELSE '00000000-0000-4000-8000-0000000000ff'::uuid END
FROM (
    SELECT n, 2 * ((n - 1) / 3) + CASE WHEN (n - 1) % 3 = 2 THEN 1 ELSE 0 END AS i,
           ((2 * ((n - 1) / 3) + CASE WHEN (n - 1) % 3 = 2 THEN 1 ELSE 0 END)::bigint * 7) % 24000 + 1 AS concern_no,
           CASE WHEN (n - 1) % 3 = 1 THEN 'EMAIL' ELSE 'IN_APP' END AS channel,
           now() - (400000 - n) * interval '157 seconds' AS at
    FROM generate_series(1, 400000) AS n) AS d;

\endif
