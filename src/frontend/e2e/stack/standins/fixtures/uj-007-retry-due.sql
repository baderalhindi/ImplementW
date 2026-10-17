-- UJ-007 (Reminder ownership): QA's "timeout" fixture (Step 18 TD-05) for one WF-15 e-mail delivery.
--
-- A delivery that failed waits RetryBaseDelay x 2^(attempt - 1) before its next attempt (1, 2, 4, 8 minutes;
-- notification-runtime.md D-13), so a relay outage reaches the fifth attempt and DEAD_LETTER after a quarter of an hour.
-- This fixture stands in for that time passing: it makes the named delivery's scheduled retry due now, and the notification
-- worker takes it on its next pass. It changes nothing else: not the attempt count, the status or the content (the delivery
-- guard allows a FAILED row's next attempt time to move), and nothing of the source record.
--
-- Variables: deliveryId. Prints one row, "<status>|<attempts>", the delivery as it stands.

UPDATE notifications.notification_delivery
SET next_attempt_at = now()
WHERE id = :'deliveryId'::uuid AND status = 'FAILED' AND next_attempt_at > now();

SELECT status || '|' || attempt_count FROM notifications.notification_delivery WHERE id = :'deliveryId'::uuid;
