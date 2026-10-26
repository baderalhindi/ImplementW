-- 43-notification-routing.sql — WF-15's routing and templates for the one source that publishes a NotificationIntent today,
-- the WF-07 concern escalation (TASK-085, UJ-007 Reminder ownership). Nothing is published in NOTIFICATION_ROUTING by db/seed
-- (notification-runtime.md F-4) and no template exists, so without this file an escalation's intent is FAILED
-- CONFIGURATION_MISSING and nobody is told.
--
-- The family CONCERN_ESCALATION (ConcernEscalated.EventFamilyCode) is mandatory, in-app and e-mail, to R03: the role
-- WORKFLOW_POLICY's CONCERN_ESCALATION_ROLE addresses escalations to (40-configuration.sql), so the Department Manager of the
-- project's department is told. No SMS: the e2e stack has no SMS provider. The templates are the two the family's channels
-- need for ManagementConcern.ConcernEscalated, in both languages, naming only the intent's parameters (concernType,
-- escalationNo, escalatedToRoleCode, severity) and the deep link. As notification-runtime.md D-8 requires they are born DRAFT,
-- validated by one person and published by another (here the e2e administrator and portfolio manager, ids of 10-people.sql).
-- The values are the notification integration host's test values, not AHDA policy (docs/testing/e2e-suite.md F-2).
--
-- A PUBLISHED version cannot change (FG-04 guard), so this file runs once per database. Synthetic kind e2e8, range 02xx.

SELECT NOT EXISTS (SELECT 1 FROM master_data_config.configuration_version WHERE id = '00000000-e2e8-4000-8000-000000000201') AS e2e_notification_routing_fresh \gset
\if :e2e_notification_routing_fresh

INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, change_summary_lang, change_summary, lifecycle_state,
                                                      created_at, created_by, updated_at, updated_by)
SELECT '00000000-e2e8-4000-8000-000000000201', f.id, 1 + (SELECT count(*) FROM master_data_config.configuration_version o WHERE o.configuration_family_id = f.id),
       'en', 'E2E data set (TASK-085): concern escalation routing', 'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f WHERE f.code = 'NOTIFICATION_ROUTING';

INSERT INTO master_data_config.notification_event_family (id, configuration_version_id, code, label_ar, label_en, is_mandatory,
                                                          created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e8-4000-8000-000000000211', '00000000-e2e8-4000-8000-000000000201', 'CONCERN_ESCALATION', 'تصعيد مسألة أو تحدٍ', 'Issue or challenge escalated',
        true, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff');

INSERT INTO master_data_config.notification_channel_rule (id, notification_event_family_id, channel, enabled_by_default, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e8-4000-8000-000000000221', '00000000-e2e8-4000-8000-000000000211', 'IN_APP', true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-e2e8-4000-8000-000000000222', '00000000-e2e8-4000-8000-000000000211', 'EMAIL', true,
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff');

INSERT INTO master_data_config.notification_recipient_rule (id, notification_event_family_id, role_id, created_at, created_by, updated_at, updated_by)
SELECT '00000000-e2e8-4000-8000-000000000231', '00000000-e2e8-4000-8000-000000000211', r.id,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.role r WHERE r.code = 'R03';

UPDATE master_data_config.configuration_version
SET lifecycle_state = 'PUBLISHED', published_by_user_id = '00000000-0000-4000-8000-0000000000ff', published_at = now() - interval '1 hour',
    effective_from = now() - interval '1 hour'
WHERE id = '00000000-e2e8-4000-8000-000000000201';

INSERT INTO notifications.notification_template (id, event_family_code, event_type, channel, version_no, subject_ar, subject_en, body_ar, body_en,
                                                 lifecycle_state, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-e2e8-4000-8000-000000000241', 'CONCERN_ESCALATION', 'ManagementConcern.ConcernEscalated', 'IN_APP', 1,
        'تصعيد رقم {{escalationNo}}', 'Escalation {{escalationNo}} of an issue or challenge',
        'صُعّدت مسألة ({{concernType}}) على مشروعك إلى الدور {{escalatedToRoleCode}}.',
        'An item ({{concernType}}) on your project was escalated to role {{escalatedToRoleCode}}.',
        'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-e2e8-4000-8000-000000000242', 'CONCERN_ESCALATION', 'ManagementConcern.ConcernEscalated', 'EMAIL', 1,
        'تصعيد رقم {{escalationNo}}', 'Escalation {{escalationNo}} of an issue or challenge',
        'صُعّدت مسألة ({{concernType}}) على مشروعك إلى الدور {{escalatedToRoleCode}}. افتحها: {{deepLink}}',
        'An item ({{concernType}}) on your project was escalated to role {{escalatedToRoleCode}}. Open it: {{deepLink}}',
        'DRAFT', now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff');

UPDATE notifications.notification_template
SET lifecycle_state = 'VALIDATED', validated_by_user_id = u.id, validated_at = now()
FROM identity_access."user" u
WHERE u.username = 'e2e.admin' AND notification_template.id IN ('00000000-e2e8-4000-8000-000000000241', '00000000-e2e8-4000-8000-000000000242');

UPDATE notifications.notification_template
SET lifecycle_state = 'PUBLISHED', published_by_user_id = u.id, published_at = now()
FROM identity_access."user" u
WHERE u.username = 'e2e.portfolio' AND notification_template.id IN ('00000000-e2e8-4000-8000-000000000241', '00000000-e2e8-4000-8000-000000000242');

\endif
