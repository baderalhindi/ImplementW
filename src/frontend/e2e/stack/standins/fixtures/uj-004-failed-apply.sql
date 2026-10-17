-- UJ-004 (Approval versus implementation): QA's "failed apply" fixture (Step 18 TD-05) for one WF-08 change authorisation.
--
-- A rebaseline under WF-03 approval applies its change authorisation as WF-11's approval reaches the schedule through the
-- outbox (change-request.md D-7, D-9; BaselineApprovalOutcomeHandler). This fixture stands in for a technical failure of that
-- application in a real environment: the authorisation's row cannot be locked when the application writes it (SQLSTATE 55P03,
-- lock_not_available, as a lock timeout raises it). The dispatch then rolls back the activation, the application and the
-- delivery mark together, counts the attempt and retries after its back-off (approval-framework.md D-8).
--
-- Only the named authorisation is affected, so journeys running beside this one are not. Variables:
--   step                  inject | observe | clear
--   changeAuthorizationId the authorisation whose application fails (inject, clear)
--   runId                 the WF-11 run whose outcome applies it (observe)
-- observe prints one row, "<attempts>|<last error or ->|<dispatched: true or false>", for the run's outcome message.

SELECT :'step' = 'inject' AS inject, :'step' = 'clear' AS clear, :'step' = 'observe' AS observe \gset

\if :inject
CREATE OR REPLACE FUNCTION public.e2e_uj004_refuse_application() RETURNS trigger
    LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status = 'ISSUED' AND NEW.status = 'APPLIED' THEN
        RAISE EXCEPTION 'e2e fixture uj-004-failed-apply: change_authorization % could not be locked for its application', OLD.id
            USING ERRCODE = 'lock_not_available';
    END IF;
    RETURN NEW;
END
$$;
SELECT format('CREATE TRIGGER %I BEFORE UPDATE ON change_request.change_authorization FOR EACH ROW WHEN (OLD.id = %L::uuid) '
              'EXECUTE FUNCTION public.e2e_uj004_refuse_application()',
              'e2e_uj004_' || replace(:'changeAuthorizationId', '-', ''), :'changeAuthorizationId') \gexec
SELECT 'injected';
\endif

\if :clear
SELECT format('DROP TRIGGER IF EXISTS %I ON change_request.change_authorization', 'e2e_uj004_' || replace(:'changeAuthorizationId', '-', '')) \gexec
SELECT 'cleared';
\endif

\if :observe
SELECT attempt_count || '|' || coalesce(last_error, '-') || '|' || (dispatched_at IS NOT NULL)
FROM common.outbox_message
WHERE message_key = 'Approval.ApprovalOutcomeRecorded:apr-' || :'runId' || '-outcome';
\endif
