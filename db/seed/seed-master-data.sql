-- seed-master-data.sql — the platform's shipped reference data (TASK-027). Record: docs/architecture/seed-data-and-integrity.md
--
-- What it holds:
--   1. the SERVICE principals (ERD D-2): the seed principal every seeded row is attributed to, the directory-sync
--      principal a directory-sourced change to a user is attributed to (TASK-028, ADR-007), and the audit-capture
--      principal an audit event with no known user is attributed to (TASK-033);
--   2. the eight canonical roles R01–R08, each with its shipped-default permission profile and that profile's
--      PUBLISHED version 1 (ADR-018, TASK-110), and the permission catalogue with the grants of those versions
--      (TASK-030) — only the rows a controlled source fixes, since Blueprint Appendix A is not in the repository;
--   3. one master data catalogue for every master data reference in the ERD (ADM-020–029), with the items a
--      controlled source fixes: the external entity types (ERD), the three governance profiles (ADR-015) and the
--      four impact dimensions (ADR-011);
--   4. the twelve configuration families of the ERD;
--   5. the risk and issue scale, seeded generically (ADR-011, OQ-006): a DRAFT version 1 of RISK_MATRIX holding
--      five probability levels and five impact levels per dimension, labelled "Level n", with no descriptions and
--      no boundaries;
--   6. the three dashboards of ADR-006 — PORTFOLIO, PROJECT, GOVERNANCE — each a PUBLISHED version 1 with its audience, default
--      landings (Blueprint §20.2) and widgets bound to registered source projections (TASK-069);
--   7. the ten reports of ADR-006, each a PUBLISHED version 1 with its audience, parameters, options and columns, every column a field of a
--      registered source projection, FG-02's catalogue entries absorbed as reports.md D-2 maps them (TASK-071);
--   8. the SCR-138 explorer's allowlist as a DRAFT version 1 of REPORT_RULES, for AHDA to review and publish (TASK-071).
-- Every label is bilingual (ADR-012).
--
-- What it does not hold, and why (record §4):
--   - materiality bands: ADR-016 fixes three bands, but the band values wait on OQ-013;
--   - governance profile settings: cadence, band count and document control level are not stated anywhere, and the
--     assignment thresholds wait on OQ-014;
--   - rating labels and the 5×5 matrix: outstanding under OQ-006;
--   - items of the other catalogues (project classification, regions, document types, …): AHDA's values, listed in
--     Blueprint v2.0 ADM-020–029, which is not in the repository;
--   - project and record statuses: these are state columns with CHECK constraints (ERD §6), not master data.
--
-- Idempotent. Every insert is keyed on the row's business key (code, or its unique key), so a second run adds no
-- row and changes no row. The seed owns STRUCTURE — codes, is_system, a role's external eligibility, a profile's
-- base role — and puts it back if it has drifted. WORDING and VALUES — labels, sort order, configuration rows — are
-- inserted once and then belong to AHDA's administrators, so a deployment never reverts an edit made on ADM-020–029.
--
-- Ids of new rows are md5(<table>:<business key>) as a uuid, the same in every environment. The roles, profiles and
-- versions keep the ids the local stack has used since TASK-014 (00000000-000n-4000-8000-00000000000n).
--
-- Pure SQL, no psql meta-command, so the release image runs it unchanged. Run it in one transaction:
--   dotnet PMPlatform.Api.dll seed                                              (each environment, after `migrate`)
--   psql "<connection>" -X -q -v ON_ERROR_STOP=1 --single-transaction -f db/seed/seed-master-data.sql     (locally)

-- 1. The SERVICE principals. A SERVICE user is a non-human principal; it holds no role and cannot sign in. The
-- directory-sync principal's id is PMPlatform.Infrastructure's UserAccessRepository.DirectorySyncPrincipalId; the
-- audit-capture principal's is PMPlatform.Application's AuditTrail.AuditCapturePrincipalId; the approval-workflow
-- principal's, which escalates overdue approval tasks, expires delegations and records outcome delivery (TASK-035), is
-- PMPlatform.Application's ApprovalServicePrincipal.Id; the outbox-dispatch principal's, the author of every dispatch
-- mark and failed attempt on common.outbox_message (TASK-035), is PMPlatform.Infrastructure's OutboxDispatcher.DispatchPrincipalId;
-- the document-scan principal's, the author of every malware-scan result on document_management.document_version
-- (TASK-037), is PMPlatform.Application's DocumentServicePrincipal.Id; the notification-dispatch principal's, the author of
-- every received intent, routing, delivery attempt and completion in the notifications schema (TASK-039), is
-- PMPlatform.Application's NotificationServicePrincipal.Id; the risk-review principal's, which expires risk acceptances and
-- returns their risks for review (TASK-055), is PMPlatform.Application's RiskServicePrincipal.Id; the suspension-activation
-- principal's, which effects approved suspension and resumption requests on their effective date (TASK-062), is
-- PMPlatform.Application's SuspensionServicePrincipal.Id; the closeout-activation principal's, which effects approved
-- completion and closure cases (TASK-063), is PMPlatform.Application's CloseoutServicePrincipal.Id; the report-generation principal's,
-- which validates, generates and expires FG-02's report jobs and their outputs (TASK-071), is PMPlatform.Application's
-- ReportServicePrincipal.Id.
INSERT INTO identity_access."user" (id, user_type, username, display_name, email, preferred_language, status, created_at, created_by, updated_at, updated_by)
VALUES ('00000000-0000-4000-8000-0000000000ff', 'SERVICE', 'svc.platform-seed', 'Platform seed (service principal)', 'svc.platform-seed@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fe', 'SERVICE', 'svc.directory-sync', 'Directory synchronisation (service principal)', 'svc.directory-sync@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fd', 'SERVICE', 'svc.audit-capture', 'Audit capture (service principal)', 'svc.audit-capture@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fc', 'SERVICE', 'svc.approval-workflow', 'Approval workflow (service principal)', 'svc.approval-workflow@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fb', 'SERVICE', 'svc.outbox-dispatch', 'Outbox dispatch (service principal)', 'svc.outbox-dispatch@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000fa', 'SERVICE', 'svc.document-scan', 'Document malware scan (service principal)', 'svc.document-scan@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000f9', 'SERVICE', 'svc.notification-dispatch', 'Notification dispatch (service principal)', 'svc.notification-dispatch@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000f8', 'SERVICE', 'svc.risk-review', 'Risk acceptance review (service principal)', 'svc.risk-review@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000f7', 'SERVICE', 'svc.suspension-activation', 'Suspension activation (service principal)', 'svc.suspension-activation@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000f6', 'SERVICE', 'svc.closeout-activation', 'Completion and closure activation (service principal)', 'svc.closeout-activation@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'),
       ('00000000-0000-4000-8000-0000000000f5', 'SERVICE', 'svc.report-generation', 'Report generation (service principal)', 'svc.report-generation@pmplatform.invalid', 'en', 'ACTIVE',
        now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff')
ON CONFLICT (id) DO NOTHING;

-- 2. Roles R01–R08. Codes are canonical (ERD F-080). The labels are PROVISIONAL (record F-1): Blueprint Appendix A
-- is not in the repository, so they follow the role dashboards DSH-001–008. R04 and R08 may be held by an external
-- entity's people (ADR-013).
INSERT INTO identity_access.role AS r (id, code, name_ar, name_en, is_system, is_external_eligible, created_at, created_by, updated_at, updated_by)
SELECT v.id::uuid, v.code, v.name_ar, v.name_en, true, v.is_external_eligible,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('00000000-0000-4000-8000-000000000001', 'R01', 'مدير النظام',      'System Administrator', false),
    ('00000000-0000-4000-8000-000000000002', 'R02', 'مدير المحفظة',     'Portfolio Manager',    false),
    ('00000000-0000-4000-8000-000000000003', 'R03', 'مدير الإدارة',     'Department Manager',   false),
    ('00000000-0000-4000-8000-000000000004', 'R04', 'مدير المشروع',     'Project Manager',      true),
    ('00000000-0000-4000-8000-000000000005', 'R05', 'ضابط الاتصال',     'Liaison',              false),
    ('00000000-0000-4000-8000-000000000006', 'R06', 'مستعرض',           'Viewer',               false),
    ('00000000-0000-4000-8000-000000000007', 'R07', 'تنفيذي',           'Executive',            false),
    ('00000000-0000-4000-8000-000000000008', 'R08', 'مستخدم جهة خارجية', 'External Entity User', true)
) AS v (id, code, name_ar, name_en, is_external_eligible)
ON CONFLICT (code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    is_external_eligible = EXCLUDED.is_external_eligible,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (r.is_system, r.is_external_eligible) IS DISTINCT FROM (EXCLUDED.is_system, EXCLUDED.is_external_eligible);

-- The shipped-default profile of each role: code <role>-DEFAULT, id = the role's id with the second group 0001.
INSERT INTO identity_access.permission_profile AS p (id, code, name_ar, name_en, base_role_id, is_shipped_default, created_at, created_by, updated_at, updated_by)
SELECT overlay(r.id::text placing '0001' from 10 for 4)::uuid, r.code || '-DEFAULT',
       r.name_ar || ' — الملف الافتراضي', r.name_en || ' — shipped default', r.id, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.role r
WHERE r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
ON CONFLICT (code) DO UPDATE SET
    base_role_id = EXCLUDED.base_role_id,
    is_shipped_default = EXCLUDED.is_shipped_default,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (p.base_role_id, p.is_shipped_default) IS DISTINCT FROM (EXCLUDED.base_role_id, EXCLUDED.is_shipped_default);

-- Version 1 of each shipped-default profile, PUBLISHED, so an assignment has a version to bind to (ADR-018). Never
-- updated: a PUBLISHED version is immutable.
INSERT INTO identity_access.permission_profile_version (id, permission_profile_id, version_no, lifecycle_state, published_at, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT overlay(p.id::text placing '0002' from 10 for 4)::uuid, p.id, 1, 'PUBLISHED', now(), 'Shipped default (seed).', 'en',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM identity_access.permission_profile p
JOIN identity_access.role r ON r.id = p.base_role_id
WHERE p.code = r.code || '-DEFAULT'
  AND r.code IN ('R01', 'R02', 'R03', 'R04', 'R05', 'R06', 'R07', 'R08')
ON CONFLICT (permission_profile_id, version_no) DO NOTHING;

-- The permission catalogue (ERD F-081) and the grants of the shipped-default versions: PMPlatform.Application's
-- PermissionCatalogue, row for row (SeedDataTests checks it). Only rows a controlled source fixes for all eight roles:
-- ADM-041 is R01's (TASK-028); ADM-002–013 are R01's (TASK-032: "reachable only for R01 per RBAC", TASK-031); FG-04
-- master data and configuration are R01's by delivery-team decision, pending Appendix A (TASK-034 record F-1); ADR-019 grants Personalize Layout and Compose Report to R02, R03 and R07, OWN because
-- each acts on the holder's own layout or report definition. APPROVAL_VIEW and APPROVAL_DECIDE (TASK-035) are in the
-- catalogue with no grant: which roles decide approvals is Appendix A's (approval-framework.md F-1). DOCUMENT_VIEW and
-- DOCUMENT_UPLOAD go to R04 at ENTITY: ADR-013's amendment to TASK-037 lets an entity Project Manager upload, view and see
-- version history on their own project within classification rules; ENTITY reaches only a holder with an entity, and the
-- external holder's per-project assignment keeps it to that project. DOCUMENT_MANAGE and every other document grant wait
-- for Appendix A (document-management.md F-1). PROJECT_VIEW and PROJECT_REGISTER (TASK-041) go to R04 and R08 at ENTITY:
-- ADR-013's amendment to TASK-041 lets external entity users create a project draft for their own entity, and entities
-- see their own projects. PROJECT_REVIEW and PROJECT_ACTIVATE are AHDA's gates and wait for Appendix A
-- (project-registration.md F-1). PROGRESS_VIEW and PROGRESS_SUBMIT (TASK-044) go to R04 at OWN, and PROGRESS_VIEW to R08 at
-- ENTITY: ADR-013's amendment to TASK-044 lets an assigned entity Project Manager submit progress on their own project, and
-- lets entities see progress and health on their own projects; a project's owner is its Project Manager, so OWN reaches
-- exactly the projects the holder manages. PROGRESS_REVIEW is AHDA's gate and waits for Appendix A (progress-update.md
-- F-2). SCHEDULE_VIEW and SCHEDULE_EDIT (TASK-046) go to R04 at OWN: WF-03's functional specification §3 makes the Project
-- Manager the schedule's owner — building it, keeping its forecast, submitting its baselines — and OWN reaches exactly the
-- projects the holder manages, internal or entity (ADR-013). Baseline approval is WF-11's APPROVAL_DECIDE; the other roles'
-- schedule grants wait for Appendix A (schedule-baseline.md F-2). TASK_VIEW, TASK_UPDATE, TASK_MANAGE and TASK_REOPEN
-- (TASK-048) go to R04 at OWN: the Project Manager plans the project's tasks, may edit a task's actual percentage (ADR-009)
-- and holds the controlled reopen, on the projects the holder manages, internal or entity (ADR-013's amendment to
-- TASK-048). The task owners' ASSIGNED grants wait for Appendix A (project-task.md F-2). MILESTONE_VIEW and MILESTONE_SUBMIT
-- (TASK-050) go to R04 at OWN: ADR-013's amendment to TASK-050 lets an entity Project Manager submit milestone achievement
-- claims on their own project, and OWN reaches the projects the holder manages, internal or entity. Acceptance stays with
-- WF-05, decided through WF-11's APPROVAL_DECIDE; the other roles' milestone grants wait for Appendix A
-- (milestone-achievement.md F-2). The five WF-06 permissions (TASK-055) — RISK_VIEW, RISK_MANAGE, RISK_ASSESS, RISK_ACCEPT,
-- RISK_REOPEN — ship with RISK_VIEW and RISK_MANAGE to R04 at OWN: ADR-013's amendment to TASK-055 lets entity Project
-- Managers register and update risks on their own project, and OWN reaches the projects the holder manages, internal or
-- entity. Rating and acceptance authority is unchanged — the application refuses RISK_ASSESS and RISK_ACCEPT to an external
-- user whatever they hold — and those grants, with the reopen, wait for Appendix A (risk-management.md F-2). The five WF-07
-- permissions (TASK-057) — CONCERN_VIEW, CONCERN_RAISE, CONCERN_MANAGE, CONCERN_ESCALATE, CONCERN_ESCALATION_RESOLVE — ship with
-- CONCERN_VIEW and CONCERN_RAISE to R08 at ENTITY and to R04 at OWN: ADR-013's amendment to TASK-057 lets entities raise a blocker or
-- an issue on their own project and see its status (intake and visibility, not escalation). WF-07's specification §3 makes the
-- Project Manager the primary manager of a project's issues and challenges, who escalates them: CONCERN_MANAGE and CONCERN_ESCALATE
-- go to R04 at OWN, and the application refuses both to an external user whatever they hold. §3 gives the Department Manager
-- escalation handling: CONCERN_VIEW and CONCERN_ESCALATION_RESOLVE go to R03 at DEPT. The rest wait for Appendix A
-- (management-concern.md F-2). The four WF-08 permissions (TASK-060) — CHANGE_REQUEST_VIEW, CHANGE_REQUEST_RAISE,
-- CHANGE_REQUEST_REVIEW, CHANGE_REQUEST_IMPLEMENT — ship with CHANGE_REQUEST_VIEW and CHANGE_REQUEST_RAISE to R04 at OWN: ADR-013's
-- amendment to TASK-060 lets entity Project Managers raise a change request, and OWN reaches the projects the holder manages, internal
-- or entity. Materiality, approval and implementation remain AHDA's: WF-08's specification §3 makes the Project Manager the
-- coordinator of a change's implementation, so CHANGE_REQUEST_IMPLEMENT goes to R04 at OWN and the application refuses it to an
-- external user whatever they hold; §3 makes the Department Manager the reviewer, so CHANGE_REQUEST_VIEW and CHANGE_REQUEST_REVIEW go
-- to R03 at DEPT. Approval is WF-11's APPROVAL_DECIDE. The rest wait for Appendix A (change-request.md F-2). The four WF-09
-- permissions (TASK-062) — SUSPENSION_VIEW, SUSPENSION_RAISE, SUSPENSION_REVIEW, SUSPENSION_ACTIVATE — ship with SUSPENSION_VIEW and
-- SUSPENSION_RAISE to R04 at OWN: WF-09's specification §3 makes the Project Manager the one who prepares and submits a suspension or
-- resumption request, and ADR-013 has the entity fill in the information and AHDA approve; OWN reaches the projects the holder
-- manages, internal or entity. §3 makes the Department Manager the reviewer, so SUSPENSION_VIEW and SUSPENSION_REVIEW go to R03 at
-- DEPT. Approval is WF-11's APPROVAL_DECIDE. Activation is WF-09's own service by default (§14), so SUSPENSION_ACTIVATE ships to no
-- role; it and the rest wait for Appendix A (suspension.md F-2). The five WF-10 permissions (TASK-063) — CLOSEOUT_VIEW,
-- CLOSEOUT_RAISE, CLOSEOUT_REVIEW, CLOSEOUT_WAIVE, CLOSEOUT_ACTIVATE — ship with CLOSEOUT_VIEW and CLOSEOUT_RAISE to R04 at OWN: WF-10's
-- specification makes the Project Manager the one who prepares completion and closure, and ADR-013 has the entity fill in the
-- information and AHDA approve. The Department Manager reviews the immutable submission and its readiness exceptions (US-CLO-DM-002
-- to -004), so CLOSEOUT_VIEW, CLOSEOUT_REVIEW and CLOSEOUT_WAIVE go to R03 at DEPT. Approval is WF-11's APPROVAL_DECIDE. Activation is
-- WF-10's own service by default (§13: "System/service"), so CLOSEOUT_ACTIVATE ships to no role; it and the rest wait for Appendix A
-- (closure.md F-2). The five WF-13 permissions (TASK-066) — EXTERNAL_REQUEST_VIEW, EXTERNAL_REQUEST_MANAGE,
-- EXTERNAL_CONTRIBUTION_RESPOND, EXTERNAL_CONTRIBUTION_REVIEW, EXTERNAL_CONTRIBUTION_APPLY — follow WF-13's specification §13: the
-- Project Manager requests, reviews when assigned and, as the source owner, applies accepted answers (US-EXT-PM-001, -012 to -017), at
-- OWN; the Department Manager requests and reviews when assigned (US-EXT-DM-003, -006, -009), at DEPT; the external entity user sees
-- its own entity's requests at ENTITY — the per-project assignment narrows it to that project — and answers only the requests it is
-- named on, at ASSIGNED (EXT-CC-03, EXT-CC-04). The application refuses requesting, reviewing and applying to an external user whatever
-- they hold: "AHDA issues update requests" and reviews them (TASK-066 gate decision; ADR-013). The same gate decision limits an
-- external contributor's direct source action to assigned tasks (WF-13 Path A): TASK_VIEW and TASK_UPDATE go to R08 at ASSIGNED, which
-- reaches the WF-04 tasks the holder owns and nothing else. The rest, with R05's liaison grants, wait for Appendix A
-- (external-participation.md F-2). The eight WF-14
-- permissions (TASK-052) — FINANCIAL_VIEW, FINANCIAL_SUBMIT,
-- FINANCIAL_REVIEW, FINANCIAL_SOURCE_MANAGE, KPI_VIEW, KPI_MANAGE, KPI_RECORD, KPI_REVIEW — ship with FINANCIAL_VIEW and KPI_VIEW
-- to R04 and R08 at ENTITY: ADR-013's amendment to TASK-052 lets an entity see budget, expenditure and KPI status for its own
-- project, masked by audience (ADR-010); ENTITY reaches only a holder with an entity. Entering, reviewing and source
-- configuration wait for Appendix A (financial-kpi.md F-2). The four WF-15 permissions (TASK-039) — notification templates and the
-- delivery operations view with dead-letter redrive — go to R01 at ALL by delivery-team decision, as FG-04 did
-- (notification-runtime.md F-1): templates and deliveries are platform-wide, with no record to scope. A person's own
-- inbox and preferences need no permission. Blueprint Appendix A, the rest of the matrix, is not in
-- the repository (record F-1).
--
-- The grants go into version 1 because no environment has run this seed with version 1 in use (none is provisioned).
-- Once one has, a change to the shipped grants is a new version and a migration of the assignments (TASK-110), not
-- an edit here: a PUBLISHED version is immutable (seed record F-12).
INSERT INTO identity_access.permission AS p (id, code, name_ar, name_en, permission_group, is_privileged, created_at, created_by, updated_at, updated_by)
SELECT md5('permission:' || v.code)::uuid, v.code, v.name_ar, v.name_en, v.permission_group, v.is_privileged,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('IDENTITY_INTEGRATION_MANAGE', 'إدارة تكامل الهوية', 'Manage identity integration', 'IDENTITY_ACCESS', true),
    ('USER_VIEW',                   'عرض المستخدمين',     'View users',                  'IDENTITY_ACCESS', false),
    ('USER_MANAGE',                 'إدارة المستخدمين',    'Manage users',                'IDENTITY_ACCESS', true),
    ('ROLE_VIEW',                   'عرض الأدوار والصلاحيات', 'View roles and permissions', 'IDENTITY_ACCESS', false),
    ('ROLE_MANAGE',                 'إدارة الأدوار',       'Manage roles',                'IDENTITY_ACCESS', true),
    ('ROLE_ASSIGN',                 'إسناد الأدوار',       'Assign roles',                'IDENTITY_ACCESS', true),
    ('ORGANIZATION_VIEW',           'عرض الهيكل التنظيمي', 'View organization structure', 'IDENTITY_ACCESS', false),
    ('ORGANIZATION_MANAGE',         'إدارة الهيكل التنظيمي', 'Manage organization structure', 'IDENTITY_ACCESS', true),
    ('MASTER_DATA_VIEW',            'عرض البيانات المرجعية', 'View master data',           'MASTER_DATA_CONFIG', false),
    ('MASTER_DATA_MANAGE',          'إدارة البيانات المرجعية', 'Manage master data',       'MASTER_DATA_CONFIG', true),
    ('CONFIGURATION_VIEW',          'عرض الإعدادات',       'View configuration',          'MASTER_DATA_CONFIG', false),
    ('CONFIGURATION_MANAGE',        'إدارة الإعدادات',      'Manage configuration',        'MASTER_DATA_CONFIG', true),
    ('LAYOUT_PERSONALIZE',          'تخصيص التخطيط',      'Personalize layout',          'DASHBOARDS',      false),
    ('REPORT_COMPOSE',              'إعداد التقارير',      'Compose report',              'REPORTS',         false),
    ('REPORT_EXPORT',               'تصدير التقارير',      'Export report',               'REPORTS',         false),
    ('APPROVAL_VIEW',               'عرض الموافقات',       'View approvals',              'APPROVAL',        false),
    ('APPROVAL_DECIDE',             'البت في الموافقات',    'Decide approvals',            'APPROVAL',        false),
    ('DOCUMENT_VIEW',               'عرض الوثائق',         'View documents',              'DOCUMENT_MANAGEMENT', false),
    ('DOCUMENT_UPLOAD',             'رفع الوثائق',         'Upload documents',            'DOCUMENT_MANAGEMENT', false),
    ('DOCUMENT_MANAGE',             'إدارة الوثائق',        'Manage documents',            'DOCUMENT_MANAGEMENT', false),
    ('PROJECT_VIEW',                'عرض المشاريع',        'View projects',               'PROJECT',         false),
    ('PROJECT_REGISTER',            'تسجيل المشاريع',      'Register projects',           'PROJECT',         false),
    ('PROJECT_REVIEW',              'مراجعة تسجيل المشاريع', 'Review project registrations', 'PROJECT',       false),
    ('PROJECT_ACTIVATE',            'تفعيل المشاريع',      'Activate projects',           'PROJECT',         false),
    ('PROGRESS_VIEW',               'عرض تقدم المشاريع',    'View project progress',       'PROGRESS',        false),
    ('PROGRESS_SUBMIT',             'رفع تقدم المشاريع',    'Submit project progress',     'PROGRESS',        false),
    ('PROGRESS_REVIEW',             'مراجعة تقدم المشاريع ونشره', 'Review and publish project progress', 'PROGRESS', false),
    ('SCHEDULE_VIEW',               'عرض الجداول الزمنية',  'View project schedules',      'SCHEDULE',        false),
    ('SCHEDULE_EDIT',               'إعداد الجداول الزمنية والخطوط الأساسية', 'Build schedules and baselines', 'SCHEDULE', false),
    ('TASK_VIEW',                   'عرض المهام',          'View tasks',                  'PROJECT_TASK',    false),
    ('TASK_UPDATE',                 'تحديث تنفيذ المهام',   'Update task execution',       'PROJECT_TASK',    false),
    ('TASK_MANAGE',                 'إدارة المهام',         'Manage tasks',                'PROJECT_TASK',    false),
    ('TASK_REOPEN',                 'إعادة فتح المهام المكتملة', 'Reopen completed tasks', 'PROJECT_TASK',    false),
    ('MILESTONE_VIEW',              'عرض إنجاز المعالم',    'View milestone achievements', 'MILESTONE',       false),
    ('MILESTONE_SUBMIT',            'تقديم إنجاز المعالم',  'Submit milestone achievements', 'MILESTONE',     false),
    ('RISK_VIEW',                   'عرض سجل المخاطر',      'View the risk register',      'RISK',            false),
    ('RISK_MANAGE',                 'إدارة المخاطر',        'Register and manage risks',   'RISK',            false),
    ('RISK_ASSESS',                 'تقييم المخاطر',        'Assess and rate risks',       'RISK',            false),
    ('RISK_ACCEPT',                 'قبول المخاطر',         'Accept risks until an expiry', 'RISK',           false),
    ('RISK_REOPEN',                 'إعادة فتح المخاطر المغلقة', 'Reopen closed risks',     'RISK',            false),
    ('CONCERN_VIEW',                'عرض المشكلات والتحديات', 'View issues and challenges', 'MANAGEMENT_CONCERN', false),
    ('CONCERN_RAISE',               'رفع المشكلات والتحديات', 'Raise issues and challenges', 'MANAGEMENT_CONCERN', false),
    ('CONCERN_MANAGE',              'إدارة المشكلات والتحديات', 'Manage issues and challenges', 'MANAGEMENT_CONCERN', false),
    ('CONCERN_ESCALATE',            'تصعيد المشكلات والتحديات', 'Escalate issues and challenges', 'MANAGEMENT_CONCERN', false),
    ('CONCERN_ESCALATION_RESOLVE',  'معالجة التصعيدات',     'Resolve escalations',         'MANAGEMENT_CONCERN', false),
    ('CHANGE_REQUEST_VIEW',         'عرض طلبات التغيير',    'View change requests',        'CHANGE_REQUEST',  false),
    ('CHANGE_REQUEST_RAISE',        'رفع طلبات التغيير',    'Raise change requests',       'CHANGE_REQUEST',  false),
    ('CHANGE_REQUEST_REVIEW',       'مراجعة طلبات التغيير', 'Review change requests',      'CHANGE_REQUEST',  false),
    ('CHANGE_REQUEST_IMPLEMENT',    'تنفيذ طلبات التغيير',  'Implement change requests',   'CHANGE_REQUEST',  false),
    ('SUSPENSION_VIEW',             'عرض طلبات التعليق والاستئناف', 'View suspension and resumption requests', 'SUSPENSION', false),
    ('SUSPENSION_RAISE',            'رفع طلبات التعليق والاستئناف', 'Raise suspension and resumption requests', 'SUSPENSION', false),
    ('SUSPENSION_REVIEW',           'مراجعة طلبات التعليق والاستئناف', 'Review suspension and resumption requests', 'SUSPENSION', false),
    ('SUSPENSION_ACTIVATE',         'تفعيل التعليق والاستئناف المعتمد', 'Activate approved suspensions and resumptions', 'SUSPENSION', false),
    ('CLOSEOUT_VIEW',               'عرض طلبات الإنجاز والإغلاق', 'View completion and closure cases', 'CLOSEOUT', false),
    ('CLOSEOUT_RAISE',              'رفع طلبات الإنجاز والإغلاق', 'Raise completion and closure cases', 'CLOSEOUT', false),
    ('CLOSEOUT_REVIEW',             'مراجعة طلبات الإنجاز والإغلاق', 'Review completion and closure cases', 'CLOSEOUT', false),
    ('CLOSEOUT_WAIVE',              'قبول استثناءات الجاهزية والالتزامات', 'Waive readiness criteria and obligations', 'CLOSEOUT', false),
    ('CLOSEOUT_ACTIVATE',           'تفعيل الإنجاز والإغلاق المعتمد', 'Activate approved completions and closures', 'CLOSEOUT', false),
    ('EXTERNAL_REQUEST_VIEW',       'عرض طلبات تحديث الجهات الخارجية', 'View external update requests', 'EXTERNAL_PARTICIPATION', false),
    ('EXTERNAL_REQUEST_MANAGE',     'إدارة طلبات تحديث الجهات الخارجية', 'Manage external update requests', 'EXTERNAL_PARTICIPATION', false),
    ('EXTERNAL_CONTRIBUTION_RESPOND', 'الرد على طلبات التحديث', 'Respond to external update requests', 'EXTERNAL_PARTICIPATION', false),
    ('EXTERNAL_CONTRIBUTION_REVIEW', 'مراجعة مساهمات الجهات الخارجية', 'Review external contributions', 'EXTERNAL_PARTICIPATION', false),
    ('EXTERNAL_CONTRIBUTION_APPLY', 'تطبيق المساهمات المقبولة', 'Apply accepted contributions', 'EXTERNAL_PARTICIPATION', false),
    ('FINANCIAL_VIEW',              'عرض البيانات المالية', 'View financial progress',     'FINANCIAL',       false),
    ('FINANCIAL_SUBMIT',            'تقديم البيانات المالية', 'Submit financial figures',  'FINANCIAL',       false),
    ('FINANCIAL_REVIEW',            'مراجعة البيانات المالية', 'Review financial figures', 'FINANCIAL',       false),
    ('FINANCIAL_SOURCE_MANAGE',     'إدارة مصادر البيانات المالية', 'Manage financial sources', 'FINANCIAL', false),
    ('KPI_VIEW',                    'عرض مؤشرات الأداء',    'View KPI performance',        'KPI',             false),
    ('KPI_MANAGE',                  'إدارة مؤشرات الأداء',  'Manage KPI assignments and targets', 'KPI',      false),
    ('KPI_RECORD',                  'تسجيل قياسات المؤشرات', 'Record KPI measurements',    'KPI',             false),
    ('KPI_REVIEW',                  'اعتماد نشر القياسات',  'Publish KPI measurements',    'KPI',             false),
    ('NOTIFICATION_TEMPLATE_VIEW',  'عرض قوالب الإشعارات',  'View notification templates', 'NOTIFICATIONS',   false),
    ('NOTIFICATION_TEMPLATE_MANAGE', 'إدارة قوالب الإشعارات', 'Manage notification templates', 'NOTIFICATIONS', true),
    ('NOTIFICATION_DELIVERY_VIEW',  'عرض عمليات الإشعارات', 'View notification deliveries', 'NOTIFICATIONS',  false),
    ('NOTIFICATION_DELIVERY_MANAGE', 'إدارة عمليات الإشعارات', 'Manage notification deliveries', 'NOTIFICATIONS', true)
) AS v (code, name_ar, name_en, permission_group, is_privileged)
ON CONFLICT (code) DO UPDATE SET
    permission_group = EXCLUDED.permission_group,
    is_privileged = EXCLUDED.is_privileged,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE (p.permission_group, p.is_privileged) IS DISTINCT FROM (EXCLUDED.permission_group, EXCLUDED.is_privileged);

INSERT INTO identity_access.permission_profile_grant (id, permission_profile_version_id, permission_id, data_scope, created_at, created_by, updated_at, updated_by)
SELECT md5('permission_profile_grant:' || g.role_code || ':' || g.permission_code)::uuid, v.id, p.id, g.data_scope,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('R01', 'IDENTITY_INTEGRATION_MANAGE', 'ALL'),
    ('R01', 'USER_VIEW',                   'ALL'),
    ('R01', 'USER_MANAGE',                 'ALL'),
    ('R01', 'ROLE_VIEW',                   'ALL'),
    ('R01', 'ROLE_MANAGE',                 'ALL'),
    ('R01', 'ROLE_ASSIGN',                 'ALL'),
    ('R01', 'ORGANIZATION_VIEW',           'ALL'),
    ('R01', 'ORGANIZATION_MANAGE',         'ALL'),
    ('R01', 'MASTER_DATA_VIEW',            'ALL'),
    ('R01', 'MASTER_DATA_MANAGE',          'ALL'),
    ('R01', 'CONFIGURATION_VIEW',          'ALL'),
    ('R01', 'CONFIGURATION_MANAGE',        'ALL'),
    ('R01', 'NOTIFICATION_TEMPLATE_VIEW',  'ALL'),
    ('R01', 'NOTIFICATION_TEMPLATE_MANAGE', 'ALL'),
    ('R01', 'NOTIFICATION_DELIVERY_VIEW',  'ALL'),
    ('R01', 'NOTIFICATION_DELIVERY_MANAGE', 'ALL'),
    ('R02', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R02', 'REPORT_COMPOSE',              'OWN'),
    ('R04', 'DOCUMENT_VIEW',               'ENTITY'),
    ('R04', 'DOCUMENT_UPLOAD',             'ENTITY'),
    ('R04', 'PROJECT_VIEW',                'ENTITY'),
    ('R04', 'PROJECT_REGISTER',            'ENTITY'),
    ('R08', 'PROJECT_VIEW',                'ENTITY'),
    ('R08', 'PROJECT_REGISTER',            'ENTITY'),
    ('R04', 'PROGRESS_VIEW',               'OWN'),
    ('R04', 'PROGRESS_SUBMIT',             'OWN'),
    ('R08', 'PROGRESS_VIEW',               'ENTITY'),
    ('R04', 'SCHEDULE_VIEW',               'OWN'),
    ('R04', 'SCHEDULE_EDIT',               'OWN'),
    ('R04', 'TASK_VIEW',                   'OWN'),
    ('R04', 'TASK_UPDATE',                 'OWN'),
    ('R04', 'TASK_MANAGE',                 'OWN'),
    ('R04', 'TASK_REOPEN',                 'OWN'),
    ('R04', 'MILESTONE_VIEW',              'OWN'),
    ('R04', 'MILESTONE_SUBMIT',            'OWN'),
    ('R04', 'RISK_VIEW',                   'OWN'),
    ('R04', 'RISK_MANAGE',                 'OWN'),
    ('R04', 'CONCERN_VIEW',                'OWN'),
    ('R04', 'CONCERN_RAISE',               'OWN'),
    ('R04', 'CONCERN_MANAGE',              'OWN'),
    ('R04', 'CONCERN_ESCALATE',            'OWN'),
    ('R08', 'CONCERN_VIEW',                'ENTITY'),
    ('R08', 'CONCERN_RAISE',               'ENTITY'),
    ('R03', 'CONCERN_VIEW',                'DEPT'),
    ('R03', 'CONCERN_ESCALATION_RESOLVE',  'DEPT'),
    ('R04', 'CHANGE_REQUEST_VIEW',         'OWN'),
    ('R04', 'CHANGE_REQUEST_RAISE',        'OWN'),
    ('R04', 'CHANGE_REQUEST_IMPLEMENT',    'OWN'),
    ('R03', 'CHANGE_REQUEST_VIEW',         'DEPT'),
    ('R03', 'CHANGE_REQUEST_REVIEW',       'DEPT'),
    ('R04', 'SUSPENSION_VIEW',             'OWN'),
    ('R04', 'SUSPENSION_RAISE',            'OWN'),
    ('R03', 'SUSPENSION_VIEW',             'DEPT'),
    ('R03', 'SUSPENSION_REVIEW',           'DEPT'),
    ('R04', 'CLOSEOUT_VIEW',               'OWN'),
    ('R04', 'CLOSEOUT_RAISE',              'OWN'),
    ('R03', 'CLOSEOUT_VIEW',               'DEPT'),
    ('R03', 'CLOSEOUT_REVIEW',             'DEPT'),
    ('R03', 'CLOSEOUT_WAIVE',              'DEPT'),
    ('R08', 'TASK_VIEW',                   'ASSIGNED'),
    ('R08', 'TASK_UPDATE',                 'ASSIGNED'),
    ('R04', 'EXTERNAL_REQUEST_VIEW',       'OWN'),
    ('R04', 'EXTERNAL_REQUEST_MANAGE',     'OWN'),
    ('R04', 'EXTERNAL_CONTRIBUTION_REVIEW', 'OWN'),
    ('R04', 'EXTERNAL_CONTRIBUTION_APPLY', 'OWN'),
    ('R03', 'EXTERNAL_REQUEST_VIEW',       'DEPT'),
    ('R03', 'EXTERNAL_REQUEST_MANAGE',     'DEPT'),
    ('R03', 'EXTERNAL_CONTRIBUTION_REVIEW', 'DEPT'),
    ('R08', 'EXTERNAL_REQUEST_VIEW',       'ENTITY'),
    ('R08', 'EXTERNAL_CONTRIBUTION_RESPOND', 'ASSIGNED'),
    ('R04', 'FINANCIAL_VIEW',              'ENTITY'),
    ('R08', 'FINANCIAL_VIEW',              'ENTITY'),
    ('R04', 'KPI_VIEW',                    'ENTITY'),
    ('R08', 'KPI_VIEW',                    'ENTITY'),
    ('R04', 'REPORT_EXPORT',               'ENTITY'),
    ('R08', 'REPORT_EXPORT',               'ENTITY'),
    ('R03', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R03', 'REPORT_COMPOSE',              'OWN'),
    ('R07', 'LAYOUT_PERSONALIZE',          'OWN'),
    ('R07', 'REPORT_COMPOSE',              'OWN')
) AS g (role_code, permission_code, data_scope)
JOIN identity_access.role r ON r.code = g.role_code
JOIN identity_access.permission_profile pp ON pp.base_role_id = r.id AND pp.code = r.code || '-DEFAULT'
JOIN identity_access.permission_profile_version v ON v.permission_profile_id = pp.id AND v.version_no = 1
JOIN identity_access.permission p ON p.code = g.permission_code
ON CONFLICT (permission_profile_version_id, permission_id) DO NOTHING;

-- 3. Master data catalogues: one per master data reference in the ERD. validate-data-integrity.sql checks that each
-- reference column points at an item of its catalogue; the two files name the same codes.
INSERT INTO master_data_config.master_data_catalogue AS c (id, code, name_ar, name_en, is_system, created_at, created_by, updated_at, updated_by)
SELECT md5('master_data_catalogue:' || v.code)::uuid, v.code, v.name_ar, v.name_en, true,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('EXTERNAL_ENTITY_TYPE',   'نوع الجهة الخارجية',      'External entity type'),
    ('GOVERNANCE_PROFILE',     'ملف الحوكمة',             'Governance profile'),
    ('IMPACT_DIMENSION',       'بُعد الأثر',               'Impact dimension'),
    ('DATA_CLASSIFICATION',    'تصنيف البيانات',          'Data classification'),
    ('DOCUMENT_CONTROL_LEVEL', 'مستوى ضبط الوثائق',       'Document control level'),
    ('PROJECT_CLASSIFICATION', 'تصنيف المشروع',           'Project classification'),
    ('REGION',                 'المنطقة',                 'Region'),
    ('CITY',                   'المدينة',                 'City'),
    ('WORKING_CALENDAR',       'تقويم العمل',             'Working calendar'),
    ('MILESTONE_CATEGORY',     'فئة المعلم الرئيسي',      'Milestone category'),
    ('EVIDENCE_TYPE',          'نوع الدليل',              'Evidence type'),
    ('DOCUMENT_TYPE',          'نوع الوثيقة',             'Document type'),
    ('PRIORITY',               'الأولوية',                'Priority'),
    ('RISK_CATEGORY',          'فئة الخطر',               'Risk category'),
    ('CONCERN_CATEGORY',       'فئة المشكلة أو التحدي',   'Issue and challenge category'),
    ('CONCERN_SEVERITY',       'خطورة المشكلة أو التحدي', 'Issue and challenge severity'),
    ('CONTRIBUTION_TYPE',      'نوع المساهمة',            'Contribution type'),
    ('ETIMAD_COST_CATEGORY',   'فئة التكلفة في اعتماد',   'Etimad cost category'),
    ('KPI_UNIT',               'وحدة قياس المؤشر',        'KPI unit'),
    ('MEASUREMENT_FREQUENCY',  'دورية القياس',            'Measurement frequency')
) AS v (code, name_ar, name_en)
ON CONFLICT (code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE NOT c.is_system;

-- The items a controlled source fixes, PUBLISHED so forms can offer them. Reviewer and publisher stay null: the
-- author/reviewer/publisher separation (TASK-110) has no people in a seed.
INSERT INTO master_data_config.master_data_item AS i (id, catalogue_id, code, label_ar, label_en, sort_order, is_system, lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
SELECT md5('master_data_item:' || v.catalogue_code || ':' || v.code)::uuid, c.id, v.code, v.label_ar, v.label_en, v.sort_order, true, 'PUBLISHED', now(),
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    -- ERD identity_access.external_entity.entity_type_item_id
    ('EXTERNAL_ENTITY_TYPE', 'GOVERNMENT',       'جهة حكومية',       'Government',       1),
    ('EXTERNAL_ENTITY_TYPE', 'PUBLIC_AUTHORITY', 'هيئة عامة',        'Public authority', 2),
    ('EXTERNAL_ENTITY_TYPE', 'PRIVATE_COMPANY',  'شركة خاصة',        'Private company',  3),
    -- ADR-015: three governance profiles
    ('GOVERNANCE_PROFILE',   'LIGHT',            'مبسّط',            'Light',            1),
    ('GOVERNANCE_PROFILE',   'STANDARD',         'قياسي',            'Standard',         2),
    ('GOVERNANCE_PROFILE',   'FULL',             'شامل',             'Full',             3),
    -- ADR-011: cost, schedule, reputation and at least one operational dimension, shared by risks and issues
    ('IMPACT_DIMENSION',     'COST',             'التكلفة',          'Cost',             1),
    ('IMPACT_DIMENSION',     'SCHEDULE',         'الجدول الزمني',    'Schedule',         2),
    ('IMPACT_DIMENSION',     'REPUTATION',       'السمعة',           'Reputation',       3),
    ('IMPACT_DIMENSION',     'OPERATIONAL',      'التشغيل',          'Operational',      4)
) AS v (catalogue_code, code, label_ar, label_en, sort_order)
JOIN master_data_config.master_data_catalogue c ON c.code = v.catalogue_code
ON CONFLICT (catalogue_id, code) DO UPDATE SET
    is_system = EXCLUDED.is_system,
    updated_at = EXCLUDED.updated_at,
    updated_by = EXCLUDED.updated_by
WHERE NOT i.is_system;

-- 4. Configuration families (ERD MasterDataConfig). A family's value keys are fixed in code, so the family is
-- platform-owned; its versions are AHDA's.
INSERT INTO master_data_config.configuration_family (id, code, name_ar, name_en, created_at, created_by, updated_at, updated_by)
SELECT md5('configuration_family:' || v.code)::uuid, v.code, v.name_ar, v.name_en,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('GOVERNANCE_PROFILE',   'ملفات الحوكمة',              'Governance profiles'),
    ('MATERIALITY_BAND',     'نطاقات الأهمية النسبية للتغيير', 'Change materiality bands'),
    ('RISK_MATRIX',          'مصفوفة المخاطر',             'Risk matrix'),
    ('APPROVAL_AUTHORITY',   'صلاحيات الاعتماد',           'Approval authority'),
    ('NOTIFICATION_ROUTING', 'توجيه الإشعارات',            'Notification routing'),
    ('KPI_POLICY',           'سياسة مؤشرات الأداء',        'KPI policy'),
    ('PARTICIPATION',        'المشاركة الخارجية',          'External participation'),
    ('EVIDENCE_POLICY',      'سياسة الأدلة',               'Evidence policy'),
    ('FIELD_CLASSIFICATION', 'تصنيف الحقول',               'Field classification'),
    ('REPORT_RULES',         'قواعد التقارير',             'Report rules'),
    ('DASHBOARD_RULES',      'قواعد لوحات المعلومات',      'Dashboard rules'),
    ('WORKFLOW_POLICY',      'سياسة سير العمل',            'Workflow policy')
) AS v (code, name_ar, name_en)
ON CONFLICT (code) DO NOTHING;

-- 5. The risk and issue scale, generically (ADR-011; OQ-006). Version 1 of RISK_MATRIX stays DRAFT: resolution
-- reads PUBLISHED versions only and fails closed without one (TASK-034), so nothing resolves against a scale AHDA
-- has not approved. AHDA adds the descriptions, boundaries, rating labels and 5×5 mapping on FG-04 and publishes.
INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT md5('configuration_version:RISK_MATRIX:1')::uuid, f.id, 1, 'DRAFT',
       'Generic five-level scale (seed). Level descriptions, boundaries, rating labels and the 5x5 mapping are outstanding (OQ-006).', 'en',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f
WHERE f.code = 'RISK_MATRIX'
ON CONFLICT (configuration_family_id, version_no) DO NOTHING;

-- Levels are added to the seeded version only while it is DRAFT; once AHDA validates it, the seed leaves it alone.
INSERT INTO master_data_config.probability_level_definition (id, configuration_version_id, level, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('probability_level_definition:RISK_MATRIX:1:' || l)::uuid, v.id, l, 'المستوى ' || l, 'Level ' || l,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_version v
CROSS JOIN generate_series(1, 5) AS l
WHERE v.id = md5('configuration_version:RISK_MATRIX:1')::uuid
  AND v.lifecycle_state = 'DRAFT'
ON CONFLICT (configuration_version_id, level) DO NOTHING;

INSERT INTO master_data_config.impact_level_definition (id, configuration_version_id, impact_dimension_item_id, level, label_ar, label_en, created_at, created_by, updated_at, updated_by)
SELECT md5('impact_level_definition:RISK_MATRIX:1:' || d.code || ':' || l)::uuid, v.id, d.id, l, 'المستوى ' || l, 'Level ' || l,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_version v
CROSS JOIN master_data_config.master_data_item d
JOIN master_data_config.master_data_catalogue c ON c.id = d.catalogue_id
CROSS JOIN generate_series(1, 5) AS l
WHERE v.id = md5('configuration_version:RISK_MATRIX:1')::uuid
  AND v.lifecycle_state = 'DRAFT'
  AND c.code = 'IMPACT_DIMENSION'
  AND d.code IN ('COST', 'SCHEDULE', 'REPUTATION', 'OPERATIONAL')
ON CONFLICT (configuration_version_id, impact_dimension_item_id, level) DO NOTHING;

-- 6. The three dashboards of ADR-006 (TASK-069): PORTFOLIO, PROJECT and GOVERNANCE, each as a PUBLISHED version 1, with the audience and
-- default landings of Blueprint §20.2 and widgets bound to registered projections only (PMPlatform.Application's DashboardProjections;
-- DashboardSeedTests checks every one). FG-01's twelve DSH definitions are absorbed into them (dashboards.md D-2): PORTFOLIO is DSH-002 with
-- DSH-003, DSH-006 and DSH-007 as scope renderings, the Home of those roles, and the summary widgets of DSH-010 to DSH-012; PROJECT is
-- DSH-009 with DSH-008 as an entity's rendering of its own project (ADR-006, ADR-013, ADR-019: entity users get it and nothing further);
-- GOVERNANCE is DSH-001, DSH-004 and DSH-005. Personalisation is PORTFOLIO's alone (ADR-019). Reviewer and publisher stay null, as for
-- the seeded master data: the author/reviewer/publisher separation has no people in a seed. The structure, the labels and the layout are
-- inserted once, with their version: a change is a new version on ADM-036, never a re-seed, so a deployment never alters a published one.
INSERT INTO dashboards.dashboard_definition (id, code, version_no, name_ar, name_en, description_ar, description_en, allows_personalization,
                                             lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
SELECT md5('dashboard_definition:' || v.code || ':1')::uuid, v.code, 1, v.name_ar, v.name_en, v.description_ar, v.description_en, v.allows_personalization,
       'PUBLISHED', now(), now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO',  'لوحة المحفظة', 'Portfolio Dashboard',
     'المشاريع ضمن نطاقك المصرح به: دورة الحياة والصحة والجدول الزمني والمخاطر والمؤشرات والوضع المالي والتقارير.',
     'The projects in your authorised scope: lifecycle, health, schedule, risk, KPI, financial and reporting position.', true),
    ('PROJECT',    'لوحة المشروع', 'Project Dashboard',
     'مشروع واحد في نظرة عامة على المشروع: الصحة والتقدم والجدول الزمني والمخاطر والمؤشرات والوضع المالي.',
     'One project in its Project Overview: health, progress, schedule, risk, KPI and financial position.', false),
    ('GOVERNANCE', 'لوحة الحوكمة', 'Governance Dashboard',
     'الالتزامات ومتطلبات المتابعة: اكتمال التقارير ومراجعات المخاطر وإعداد لوحات المعلومات.',
     'Obligations and attention: reporting completeness, risk reviews and dashboard configuration.', false)
) AS v (code, name_ar, name_en, description_ar, description_en, allows_personalization)
ON CONFLICT (code, version_no) DO NOTHING;

-- The audience of version 1, written with it and never after: a version's audience changes only while it is a DRAFT.
INSERT INTO dashboards.dashboard_audience_role (id, dashboard_definition_id, role_id, is_default_landing, created_at, created_by, updated_at, updated_by)
SELECT md5('dashboard_audience_role:' || v.code || ':1:' || v.role_code)::uuid, d.id, r.id, v.is_default_landing,
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO',  'R02', true),  ('PORTFOLIO',  'R03', true),  ('PORTFOLIO',  'R06', true),  ('PORTFOLIO',  'R07', true),
    ('PROJECT',    'R02', false), ('PROJECT',    'R03', false), ('PROJECT',    'R04', false), ('PROJECT',    'R05', false),
    ('PROJECT',    'R06', false), ('PROJECT',    'R07', false), ('PROJECT',    'R08', true),
    ('GOVERNANCE', 'R01', true),  ('GOVERNANCE', 'R02', false), ('GOVERNANCE', 'R03', false), ('GOVERNANCE', 'R04', true),
    ('GOVERNANCE', 'R05', true),  ('GOVERNANCE', 'R07', false)
) AS v (code, role_code, is_default_landing)
JOIN dashboards.dashboard_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
JOIN identity_access.role r ON r.code = v.role_code
WHERE NOT EXISTS (SELECT 1 FROM dashboards.dashboard_audience_role a WHERE a.dashboard_definition_id = d.id)
ON CONFLICT (dashboard_definition_id, role_id) DO NOTHING;

-- The widgets of version 1, likewise. Each names a registered projection; its layout is a twelve-column grid, one row high.
INSERT INTO dashboards.dashboard_widget (id, dashboard_definition_id, code, title_ar, title_en, widget_type, source_projection_code, is_optional_visibility,
                                         layout_row, layout_column, layout_span, created_at, created_by, updated_at, updated_by)
SELECT md5('dashboard_widget:' || v.code || ':1:' || v.widget_code)::uuid, d.id, v.widget_code, v.title_ar, v.title_en, v.widget_type, v.projection,
       v.is_optional, v.layout_row, v.layout_column, v.layout_span, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO', 'PROJECTS_BY_LIFECYCLE',  'المشاريع حسب حالة دورة الحياة',  'Projects by lifecycle state',        'STATUS_DISTRIBUTION', 'PROJECT.LIFECYCLE_STATE',                    false, 1, 1, 6),
    ('PORTFOLIO', 'PUBLISHED_HEALTH',       'الصحة العامة المنشورة',           'Published Overall Project Health',   'STATUS_DISTRIBUTION', 'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',       false, 1, 7, 6),
    ('PORTFOLIO', 'CURRENT_HEALTH',         'الصحة العامة الحالية',            'Current Overall Project Health',     'STATUS_DISTRIBUTION', 'PROGRESS.PROJECT_HEALTH_STATUS',             true,  2, 1, 4),
    ('PORTFOLIO', 'SCHEDULE_HEALTH',        'صحة الجدول الزمني الحالية',       'Current schedule health',            'STATUS_DISTRIBUTION', 'SCHEDULE.SCHEDULE_HEALTH_STATUS',            true,  2, 5, 4),
    ('PORTFOLIO', 'REPORTING_COMPLETENESS', 'اكتمال التقارير',                 'Reporting completeness',             'STATUS_DISTRIBUTION', 'PROGRESS.REPORTING_COMPLETENESS',            true,  2, 9, 4),
    ('PORTFOLIO', 'RISK_EXPOSURE',          'المخاطر المفتوحة حسب التصنيف',    'Open risks by rating',               'BAR_COLUMN',          'RISK.RISK_EXPOSURE',                         true,  3, 1, 6),
    ('PORTFOLIO', 'KPI_CONDITION',          'حالة مؤشرات الأداء',              'KPI condition',                      'STATUS_DISTRIBUTION', 'FINANCIAL_KPI.KPI_CONDITION',                true,  3, 7, 6),
    ('PORTFOLIO', 'PUBLISHED_FINANCIALS',   'الوضع المالي المنشور',            'Published financial position',       'METRIC_CARD',         'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT', true,  4, 1, 6),
    ('PORTFOLIO', 'CURRENT_FINANCIALS',     'الوضع المالي الحالي',             'Current financial position',         'METRIC_CARD',         'FINANCIAL_KPI.FINANCIAL_POSITION',          true,  4, 7, 6),
    ('PROJECT',   'LIFECYCLE_STATE',        'حالة دورة الحياة',                'Lifecycle state',                    'METRIC_CARD',         'PROJECT.LIFECYCLE_STATE',                    false, 1, 1, 3),
    ('PROJECT',   'CURRENT_HEALTH',         'الصحة العامة الحالية',            'Current Overall Project Health',     'METRIC_CARD',         'PROGRESS.PROJECT_HEALTH_STATUS',             false, 1, 4, 3),
    ('PROJECT',   'PUBLISHED_HEALTH',       'الصحة العامة المنشورة',           'Published Overall Project Health',   'METRIC_CARD',         'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',       false, 1, 7, 3),
    ('PROJECT',   'PUBLISHED_PROGRESS',     'التقدم المنشور',                  'Published progress',                 'PROGRESS_INDICATOR',  'PROGRESS.PUBLISHED_PROGRESS_SNAPSHOT',       false, 1, 10, 3),
    ('PROJECT',   'PUBLISHED_SCHEDULE',     'صحة الجدول الزمني المنشورة',      'Published schedule health',          'METRIC_CARD',         'PROGRESS.PUBLISHED_SCHEDULE_HEALTH',         false, 2, 1, 4),
    ('PROJECT',   'SCHEDULE_HEALTH',        'صحة الجدول الزمني الحالية',       'Current schedule health',            'METRIC_CARD',         'SCHEDULE.SCHEDULE_HEALTH_STATUS',            false, 2, 5, 4),
    ('PROJECT',   'REPORTING_COMPLETENESS', 'اكتمال التقارير',                 'Reporting completeness',             'METRIC_CARD',         'PROGRESS.REPORTING_COMPLETENESS',            false, 2, 9, 4),
    ('PROJECT',   'PROGRESS_TREND',         'اتجاه التقدم المنشور',            'Published progress trend',           'LINE_TREND',          'PROGRESS.PUBLISHED_PROGRESS_HISTORY',        false, 3, 1, 12),
    ('PROJECT',   'RISK_EXPOSURE',          'المخاطر المفتوحة حسب التصنيف',    'Open risks by rating',               'STATUS_DISTRIBUTION', 'RISK.RISK_EXPOSURE',                         false, 4, 1, 6),
    ('PROJECT',   'KPI_CONDITION',          'حالة مؤشرات الأداء',              'KPI condition',                      'STATUS_DISTRIBUTION', 'FINANCIAL_KPI.KPI_CONDITION',                false, 4, 7, 6),
    ('PROJECT',   'PUBLISHED_FINANCIALS',   'الوضع المالي المنشور',            'Published financial position',       'METRIC_CARD',         'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT', false, 5, 1, 6),
    ('PROJECT',   'CURRENT_FINANCIALS',     'الوضع المالي الحالي',             'Current financial position',         'METRIC_CARD',         'FINANCIAL_KPI.FINANCIAL_POSITION',          false, 5, 7, 6),
    ('GOVERNANCE', 'REPORTING_COMPLETENESS', 'اكتمال التقارير',                'Reporting completeness',             'STATUS_DISTRIBUTION', 'PROGRESS.REPORTING_COMPLETENESS',            false, 1, 1, 6),
    ('GOVERNANCE', 'PROJECTS_BY_LIFECYCLE', 'المشاريع حسب حالة دورة الحياة',   'Projects by lifecycle state',        'STATUS_DISTRIBUTION', 'PROJECT.LIFECYCLE_STATE',                    false, 1, 7, 6),
    ('GOVERNANCE', 'RISK_EXPOSURE',         'المخاطر المفتوحة ومراجعاتها',     'Open risks and their reviews',       'BAR_COLUMN',          'RISK.RISK_EXPOSURE',                         false, 2, 1, 6),
    ('GOVERNANCE', 'KPI_CONDITION',         'حالة مؤشرات الأداء',              'KPI condition',                      'STATUS_DISTRIBUTION', 'FINANCIAL_KPI.KPI_CONDITION',                false, 2, 7, 6),
    ('GOVERNANCE', 'CONFIGURATION_BACKLOG', 'إعداد لوحات المعلومات',           'Dashboard configuration backlog',    'STATUS_DISTRIBUTION', 'DASHBOARDS.DEFINITION_BACKLOG',              false, 3, 1, 12)
) AS v (code, widget_code, title_ar, title_en, widget_type, projection, is_optional, layout_row, layout_column, layout_span)
JOIN dashboards.dashboard_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
WHERE NOT EXISTS (SELECT 1 FROM dashboards.dashboard_widget w WHERE w.dashboard_definition_id = d.id)
ON CONFLICT (dashboard_definition_id, code) DO NOTHING;

-- 7. The ten reports of ADR-006 (TASK-071), each a PUBLISHED version 1 with its audience, parameters, options and columns, every column a field of
-- FG-01's register (PMPlatform.Application's DashboardProjections; ReportRuntimeTests.TheCatalogueIsTheTenReportsOfAdr006 checks every one). FG-02's twenty-three delivered catalogue
-- entries are absorbed as reports.md D-2 maps them: whole, as an audience, or as an option (whose catalogue_entry_reference names it). R08 is in
-- the audience of ADR-013's entity report set only; R01 of none (BR-RPT-011). Inserted once with their version, as the dashboards are: a change is
-- a new version on ADM-037, never a re-seed. Generated by artifacts/task-071/seed/gen.py.
INSERT INTO reports.report_definition (id, code, version_no, name_ar, name_en, description_ar, description_en, audience_family, primary_projection_code,
                                       allows_saved_views, lifecycle_state, published_at, created_at, created_by, updated_at, updated_by)
SELECT md5('report_definition:' || v.code || ':1')::uuid, v.code, 1, v.name_ar, v.name_en, v.description_ar, v.description_en, v.audience_family,
       v.primary_projection_code, v.allows_saved_views, 'PUBLISHED', now(), now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO_SUMMARY', 'ملخص المحفظة', 'Portfolio Summary',
     'المشاريع ضمن نطاقك: دورة الحياة والصحة المنشورة والحالية والجدول الزمني والتقارير والمخاطر ومؤشرات الأداء والحالة المالية المنشورة.',
     'The projects in your scope: lifecycle, published and current health, schedule, reporting, risk, KPI and published financial condition.',
     'PORTFOLIO', 'PROJECT.LIFECYCLE_STATE', true),
    ('PROJECT_REGISTER', 'سجل المشاريع', 'Project Register',
     'المشاريع المصرح لك بها مع الإدارة والجهة المنفذة وحالة دورة الحياة؛ وتصفيتها حسب الحالة تعطي المشاريع المعلقة أو المكتملة أو المغلقة.',
     'The projects you may see, with department, delivering entity and lifecycle state; filtered by state, the suspended, completed or closed projects.',
     'DEPARTMENT', 'PROJECT.LIFECYCLE_STATE', true),
    ('PROJECT_REPORT', 'تقرير المشروع', 'Project Report',
     'التقرير الرسمي لمشروع واحد: الهوية ودورة الحياة والصحة والتقدم والجدول الزمني والتقارير والمخاطر والقضايا والوضع المالي ومؤشرات الأداء.',
     'The formal report of one project: identity, lifecycle, health, progress, schedule, reporting, risks, issues, financial position and KPIs.',
     'PROJECT', 'PROJECT.LIFECYCLE_STATE', true),
    ('PROGRESS_REPORTING', 'حالة تقارير التقدم', 'Progress Reporting Status',
     'هل تقارير التقدم للمشاريع النشطة محدثة أم متأخرة، مع آخر صحة وتقدم منشورين. التأخر في التقارير ليس تأخراً في المشروع.',
     'Whether ACTIVE projects'' progress reporting is up to date or overdue, with the last published health and progress. Late reporting is not project delay.',
     'PROGRESS', 'PROGRESS.REPORTING_COMPLETENESS', true),
    ('PROGRESS_HISTORY', 'سجل التقدم المنشور', 'Published Progress History',
     'لقطات التقدم المنشورة لمشروع، الأقدم أولاً: سجل المصدر نفسه، لا يعاد بناؤه من البيانات الحالية.',
     'A project''s published progress snapshots, oldest first: the source''s own history, never rebuilt from current data.',
     'PROGRESS', 'PROGRESS.PUBLISHED_PROGRESS_HISTORY', true),
    ('SCHEDULE_DELIVERY', 'الجدول الزمني والتسليم', 'Schedule and Delivery',
     'صحة الجدول الزمني الحالية والمنشورة وانحراف تاريخ الانتهاء، مع المهام المفتوحة ومطالبات إنجاز المعالم المفتوحة.',
     'Current and published schedule health and finish variance, with open tasks and open milestone achievement claims.',
     'PROJECT', 'SCHEDULE.SCHEDULE_HEALTH_STATUS', true),
    ('RISK_ISSUE', 'المخاطر والقضايا', 'Risks and Issues',
     'المخاطر المفتوحة وغير المقيمة والمتأخرة المراجعة كما سجلها نظام المخاطر، مع القضايا والتحديات المفتوحة؛ دون إعادة تقييم.',
     'Open, unassessed and review-overdue risks as WF-06 recorded them, with open issues and challenges; never rescored.',
     'RISK_ISSUE', 'RISK.RISK_EXPOSURE', true),
    ('FINANCIAL_PERFORMANCE', 'الأداء المالي', 'Financial Performance',
     'الوضع المالي المنشور والحالي بالريال: الميزانية المعتمدة والصرف الفعلي والتكلفة المتوقعة والحالة المالية. بيانات حساسة تحجب حسب الفئة.',
     'The published and current financial position in SAR: approved budget, actual expenditure, forecast at completion and financial condition. Sensitive; masked by audience.',
     'FINANCIAL', 'FINANCIAL_KPI.PUBLISHED_FINANCIAL_SNAPSHOT', true),
    ('KPI_PERFORMANCE', 'أداء مؤشرات الأداء', 'KPI Performance',
     'مؤشرات الأداء النشطة حسب حالة آخر قياس منشور، واكتمال القياس والتقارير المالية وتقارير التقدم. لا تجمع مؤشرات مختلفة.',
     'Active KPIs by the condition of their latest published measurement, with measurement, financial and progress reporting completeness. Unlike KPIs are never combined.',
     'FINANCIAL', 'FINANCIAL_KPI.KPI_CONDITION', true),
    ('GOVERNANCE_CHANGE', 'الحوكمة والتغيير', 'Governance and Change',
     'طلبات التغيير قيد البت والمعتمدة وقيد التنفيذ، وطلبات التعليق والاستئناف المفتوحة؛ المعتمد ليس منفذاً، والطلب ليس تعليقاً.',
     'Change requests undecided, approved and in implementation, and open suspension or resumption requests; approved is not implemented, a request is not a suspension.',
     'EXECUTIVE', 'CHANGE_REQUEST.CHANGE_POSITION', true)
) AS v (code, name_ar, name_en, description_ar, description_en, audience_family, primary_projection_code, allows_saved_views)
ON CONFLICT (code, version_no) DO NOTHING;

-- The audience of version 1, written with it and never after.
INSERT INTO reports.report_audience_role (id, report_definition_id, role_id, created_at, created_by, updated_at, updated_by)
SELECT md5('report_audience_role:' || v.code || ':1:' || v.role_code)::uuid, d.id, r.id, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO_SUMMARY', 'R02'), ('PORTFOLIO_SUMMARY', 'R03'), ('PORTFOLIO_SUMMARY', 'R04'), ('PORTFOLIO_SUMMARY', 'R05'), ('PORTFOLIO_SUMMARY', 'R06'), ('PORTFOLIO_SUMMARY', 'R07'),
    ('PROJECT_REGISTER', 'R02'), ('PROJECT_REGISTER', 'R03'), ('PROJECT_REGISTER', 'R04'), ('PROJECT_REGISTER', 'R05'), ('PROJECT_REGISTER', 'R06'), ('PROJECT_REGISTER', 'R07'), ('PROJECT_REGISTER', 'R08'),
    ('PROJECT_REPORT', 'R02'), ('PROJECT_REPORT', 'R03'), ('PROJECT_REPORT', 'R04'), ('PROJECT_REPORT', 'R05'), ('PROJECT_REPORT', 'R06'), ('PROJECT_REPORT', 'R07'), ('PROJECT_REPORT', 'R08'),
    ('PROGRESS_REPORTING', 'R02'), ('PROGRESS_REPORTING', 'R03'), ('PROGRESS_REPORTING', 'R04'), ('PROGRESS_REPORTING', 'R05'), ('PROGRESS_REPORTING', 'R06'), ('PROGRESS_REPORTING', 'R07'), ('PROGRESS_REPORTING', 'R08'),
    ('PROGRESS_HISTORY', 'R02'), ('PROGRESS_HISTORY', 'R03'), ('PROGRESS_HISTORY', 'R04'), ('PROGRESS_HISTORY', 'R05'), ('PROGRESS_HISTORY', 'R06'), ('PROGRESS_HISTORY', 'R07'), ('PROGRESS_HISTORY', 'R08'),
    ('SCHEDULE_DELIVERY', 'R02'), ('SCHEDULE_DELIVERY', 'R03'), ('SCHEDULE_DELIVERY', 'R04'), ('SCHEDULE_DELIVERY', 'R05'), ('SCHEDULE_DELIVERY', 'R06'), ('SCHEDULE_DELIVERY', 'R07'),
    ('RISK_ISSUE', 'R02'), ('RISK_ISSUE', 'R03'), ('RISK_ISSUE', 'R04'), ('RISK_ISSUE', 'R05'), ('RISK_ISSUE', 'R06'), ('RISK_ISSUE', 'R07'),
    ('FINANCIAL_PERFORMANCE', 'R02'), ('FINANCIAL_PERFORMANCE', 'R03'), ('FINANCIAL_PERFORMANCE', 'R04'), ('FINANCIAL_PERFORMANCE', 'R05'), ('FINANCIAL_PERFORMANCE', 'R06'), ('FINANCIAL_PERFORMANCE', 'R07'), ('FINANCIAL_PERFORMANCE', 'R08'),
    ('KPI_PERFORMANCE', 'R02'), ('KPI_PERFORMANCE', 'R03'), ('KPI_PERFORMANCE', 'R04'), ('KPI_PERFORMANCE', 'R05'), ('KPI_PERFORMANCE', 'R06'), ('KPI_PERFORMANCE', 'R07'), ('KPI_PERFORMANCE', 'R08'),
    ('GOVERNANCE_CHANGE', 'R02'), ('GOVERNANCE_CHANGE', 'R03'), ('GOVERNANCE_CHANGE', 'R04'), ('GOVERNANCE_CHANGE', 'R05'), ('GOVERNANCE_CHANGE', 'R06'), ('GOVERNANCE_CHANGE', 'R07')
) AS v (code, role_code)
JOIN reports.report_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
JOIN identity_access.role r ON r.code = v.role_code
WHERE NOT EXISTS (SELECT 1 FROM reports.report_audience_role a WHERE a.report_definition_id = d.id)
ON CONFLICT (report_definition_id, role_id) DO NOTHING;

-- The columns of version 1, in order; the default columns are those shown when no choice is made.
INSERT INTO reports.report_column (id, report_definition_id, source_entity_code, field_code, label_ar, label_en, sort_order, is_default_visible,
                                   created_at, created_by, updated_at, updated_by)
SELECT md5('report_column:' || v.code || ':1:' || v.source_entity_code || '.' || v.field_code)::uuid, d.id, v.source_entity_code, v.field_code,
       v.label_ar, v.label_en, v.sort_order, v.is_default_visible, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO_SUMMARY', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('PORTFOLIO_SUMMARY', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('PORTFOLIO_SUMMARY', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('PORTFOLIO_SUMMARY', 'PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', 4, true),
    ('PORTFOLIO_SUMMARY', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 5, true),
    ('PORTFOLIO_SUMMARY', 'PROGRESS_PROJECT_HEALTH_STATUS', 'OVERALL_HEALTH', 'الصحة العامة الحالية', 'Current Overall Project Health', 6, false),
    ('PORTFOLIO_SUMMARY', 'PROGRESS_PUBLISHED_SCHEDULE_HEALTH', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني المنشورة', 'Published schedule health', 7, true),
    ('PORTFOLIO_SUMMARY', 'PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', 8, true),
    ('PORTFOLIO_SUMMARY', 'RISK_RISK_EXPOSURE', 'OPEN_RISKS', 'المخاطر المفتوحة', 'Open risks', 9, true),
    ('PORTFOLIO_SUMMARY', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_RED', 'مؤشرات حمراء', 'KPIs red', 10, false),
    ('PORTFOLIO_SUMMARY', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', 11, false),
    ('PROJECT_REGISTER', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('PROJECT_REGISTER', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('PROJECT_REGISTER', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('PROJECT_REGISTER', 'PROJECT', 'EXTERNAL_ENTITY', 'الجهة المنفذة', 'Delivering entity', 4, true),
    ('PROJECT_REGISTER', 'PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', 5, true),
    ('PROJECT_REGISTER', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 6, false),
    ('PROJECT_REGISTER', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية المنشورة', 'Published actual progress %', 7, false),
    ('PROJECT_REPORT', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('PROJECT_REPORT', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('PROJECT_REPORT', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('PROJECT_REPORT', 'PROJECT', 'EXTERNAL_ENTITY', 'الجهة المنفذة', 'Delivering entity', 4, true),
    ('PROJECT_REPORT', 'PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', 5, true),
    ('PROJECT_REPORT', 'PROGRESS_PROJECT_HEALTH_STATUS', 'OVERALL_HEALTH', 'الصحة العامة الحالية', 'Current Overall Project Health', 6, true),
    ('PROJECT_REPORT', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 7, true),
    ('PROJECT_REPORT', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية المنشورة', 'Published actual progress %', 8, true),
    ('PROJECT_REPORT', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'PLANNED_PERCENT', 'نسبة الإنجاز المخططة المنشورة', 'Published planned progress %', 9, true),
    ('PROJECT_REPORT', 'PROGRESS_PUBLISHED_SCHEDULE_HEALTH', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني المنشورة', 'Published schedule health', 10, true),
    ('PROJECT_REPORT', 'SCHEDULE_SCHEDULE_HEALTH_STATUS', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني الحالية', 'Current schedule health', 11, true),
    ('PROJECT_REPORT', 'SCHEDULE_SCHEDULE_HEALTH_STATUS', 'FINISH_VARIANCE_DAYS', 'انحراف تاريخ الانتهاء بالأيام', 'Finish variance (days)', 12, true),
    ('PROJECT_REPORT', 'PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', 13, true),
    ('PROJECT_REPORT', 'PROGRESS_REPORTING_COMPLETENESS', 'OVERDUE_PERIODS', 'الفترات المتأخرة', 'Overdue reporting periods', 14, true),
    ('PROJECT_REPORT', 'RISK_RISK_EXPOSURE', 'OPEN_RISKS', 'المخاطر المفتوحة', 'Open risks', 15, true),
    ('PROJECT_REPORT', 'MANAGEMENT_CONCERN_OPEN_CONCERNS', 'OPEN_CONCERNS', 'القضايا والتحديات المفتوحة', 'Open issues and challenges', 16, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', 17, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'APPROVED_BUDGET', 'الميزانية المعتمدة (منشور)', 'Approved budget (published)', 18, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'ACTUAL_EXPENDITURE_TO_DATE', 'الصرف الفعلي حتى تاريخه (منشور)', 'Actual expenditure to date (published)', 19, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FORECAST_AT_COMPLETION', 'التكلفة المتوقعة عند الإنجاز (منشور)', 'Forecast at completion (published)', 20, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_KPI_CONDITION', 'ACTIVE_ASSIGNMENTS', 'مؤشرات الأداء النشطة', 'Active KPIs', 21, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_GREEN', 'مؤشرات خضراء', 'KPIs green', 22, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_AMBER', 'مؤشرات كهرمانية', 'KPIs amber', 23, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_RED', 'مؤشرات حمراء', 'KPIs red', 24, true),
    ('PROJECT_REPORT', 'FINANCIAL_KPI_KPI_CONDITION', 'NOT_PUBLISHED', 'مؤشرات بلا قياس منشور', 'KPIs with no published measurement', 25, true),
    ('PROGRESS_REPORTING', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('PROGRESS_REPORTING', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('PROGRESS_REPORTING', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('PROGRESS_REPORTING', 'PROJECT', 'EXTERNAL_ENTITY', 'الجهة المنفذة', 'Delivering entity', 4, false),
    ('PROGRESS_REPORTING', 'PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', 5, true),
    ('PROGRESS_REPORTING', 'PROGRESS_REPORTING_COMPLETENESS', 'OVERDUE_PERIODS', 'الفترات المتأخرة', 'Overdue reporting periods', 6, true),
    ('PROGRESS_REPORTING', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 7, true),
    ('PROGRESS_REPORTING', 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية المنشورة', 'Published actual progress %', 8, true),
    ('PROGRESS_HISTORY', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('PROGRESS_HISTORY', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'PUBLISHED_AT', 'تاريخ النشر', 'Published at', 3, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'PERIOD_START', 'بداية الفترة', 'Period start', 4, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'PERIOD_END', 'نهاية الفترة', 'Period end', 5, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 6, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية المنشورة', 'Published actual progress %', 7, true),
    ('PROGRESS_HISTORY', 'PROGRESS_PUBLISHED_PROGRESS_HISTORY', 'PLANNED_PERCENT', 'نسبة الإنجاز المخططة المنشورة', 'Published planned progress %', 8, true),
    ('SCHEDULE_DELIVERY', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('SCHEDULE_DELIVERY', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('SCHEDULE_DELIVERY', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_SCHEDULE_HEALTH_STATUS', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني الحالية', 'Current schedule health', 4, true),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_SCHEDULE_HEALTH_STATUS', 'FINISH_VARIANCE_DAYS', 'انحراف تاريخ الانتهاء بالأيام', 'Finish variance (days)', 5, true),
    ('SCHEDULE_DELIVERY', 'PROGRESS_PUBLISHED_SCHEDULE_HEALTH', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني المنشورة', 'Published schedule health', 6, true),
    ('SCHEDULE_DELIVERY', 'PROJECT_TASK_OPEN_TASKS', 'OPEN_TASKS', 'المهام المفتوحة', 'Open tasks', 7, true),
    ('SCHEDULE_DELIVERY', 'MILESTONE_OPEN_ACHIEVEMENT_CLAIMS', 'OPEN_ACHIEVEMENT_CLAIMS', 'مطالبات إنجاز معالم مفتوحة', 'Open milestone achievement claims', 8, true),
    ('RISK_ISSUE', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('RISK_ISSUE', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('RISK_ISSUE', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('RISK_ISSUE', 'RISK_RISK_EXPOSURE', 'OPEN_RISKS', 'المخاطر المفتوحة', 'Open risks', 4, true),
    ('RISK_ISSUE', 'RISK_RISK_EXPOSURE', 'NOT_ASSESSED', 'مخاطر غير مقيمة', 'Risks not assessed', 5, true),
    ('RISK_ISSUE', 'RISK_RISK_EXPOSURE', 'REVIEW_OVERDUE', 'مراجعات مخاطر متأخرة', 'Risk reviews overdue', 6, true),
    ('RISK_ISSUE', 'MANAGEMENT_CONCERN_OPEN_CONCERNS', 'OPEN_CONCERNS', 'القضايا والتحديات المفتوحة', 'Open issues and challenges', 7, true),
    ('FINANCIAL_PERFORMANCE', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('FINANCIAL_PERFORMANCE', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('FINANCIAL_PERFORMANCE', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('FINANCIAL_PERFORMANCE', 'PROJECT', 'EXTERNAL_ENTITY', 'الجهة المنفذة', 'Delivering entity', 4, false),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', 5, true),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'APPROVED_BUDGET', 'الميزانية المعتمدة (منشور)', 'Approved budget (published)', 6, true),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'ACTUAL_EXPENDITURE_TO_DATE', 'الصرف الفعلي حتى تاريخه (منشور)', 'Actual expenditure to date (published)', 7, true),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FORECAST_AT_COMPLETION', 'التكلفة المتوقعة عند الإنجاز (منشور)', 'Forecast at completion (published)', 8, true),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_FINANCIAL_POSITION', 'FINANCIAL_CONDITION', 'الحالة المالية الحالية', 'Current financial condition', 9, false),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_FINANCIAL_POSITION', 'APPROVED_BUDGET', 'الميزانية المعتمدة (حالي)', 'Approved budget (current)', 10, false),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_FINANCIAL_POSITION', 'ACTUAL_EXPENDITURE_TO_DATE', 'الصرف الفعلي حتى تاريخه (حالي)', 'Actual expenditure to date (current)', 11, false),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_KPI_FINANCIAL_POSITION', 'FORECAST_AT_COMPLETION', 'التكلفة المتوقعة عند الإنجاز (حالي)', 'Forecast at completion (current)', 12, false),
    ('KPI_PERFORMANCE', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('KPI_PERFORMANCE', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('KPI_PERFORMANCE', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'ACTIVE_ASSIGNMENTS', 'مؤشرات الأداء النشطة', 'Active KPIs', 4, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_GREEN', 'مؤشرات خضراء', 'KPIs green', 5, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_AMBER', 'مؤشرات كهرمانية', 'KPIs amber', 6, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_RED', 'مؤشرات حمراء', 'KPIs red', 7, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'RAG_UNKNOWN', 'مؤشرات غير معروفة الحالة', 'KPIs unknown', 8, false),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_KPI_CONDITION', 'NOT_PUBLISHED', 'مؤشرات بلا قياس منشور', 'KPIs with no published measurement', 9, true),
    ('KPI_PERFORMANCE', 'PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', 10, true),
    ('KPI_PERFORMANCE', 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', 11, true),
    ('GOVERNANCE_CHANGE', 'PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', 1, true),
    ('GOVERNANCE_CHANGE', 'PROJECT', 'TITLE', 'اسم المشروع', 'Project', 2, true),
    ('GOVERNANCE_CHANGE', 'PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', 3, true),
    ('GOVERNANCE_CHANGE', 'PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', 4, true),
    ('GOVERNANCE_CHANGE', 'CHANGE_REQUEST_CHANGE_POSITION', 'UNDECIDED', 'طلبات تغيير قيد البت', 'Change requests undecided', 5, true),
    ('GOVERNANCE_CHANGE', 'CHANGE_REQUEST_CHANGE_POSITION', 'APPROVED_NOT_STARTED', 'طلبات تغيير معتمدة لم يبدأ تنفيذها', 'Change requests approved, not started', 6, true),
    ('GOVERNANCE_CHANGE', 'CHANGE_REQUEST_CHANGE_POSITION', 'IN_IMPLEMENTATION', 'طلبات تغيير قيد التنفيذ', 'Change requests in implementation', 7, true),
    ('GOVERNANCE_CHANGE', 'SUSPENSION_OPEN_REQUESTS', 'OPEN_REQUESTS', 'طلبات تعليق أو استئناف مفتوحة', 'Open suspension or resumption requests', 8, true)
) AS v (code, source_entity_code, field_code, label_ar, label_en, sort_order, is_default_visible)
JOIN reports.report_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
WHERE NOT EXISTS (SELECT 1 FROM reports.report_column c WHERE c.report_definition_id = d.id)
ON CONFLICT (report_definition_id, source_entity_code, field_code) DO NOTHING;

-- The parameters of version 1: a department or a project narrows the rows; an OPTION filters the column it is bound to.
INSERT INTO reports.report_parameter (id, report_definition_id, code, label_ar, label_en, data_type, is_required, sort_order, source_entity_code, field_code,
                                      created_at, created_by, updated_at, updated_by)
SELECT md5('report_parameter:' || v.code || ':1:' || v.parameter_code)::uuid, d.id, v.parameter_code, v.label_ar, v.label_en, v.data_type, v.is_required,
       v.sort_order, v.source_entity_code, v.field_code, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO_SUMMARY', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('PORTFOLIO_SUMMARY', 'PUBLISHED_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', 'OPTION', false, 2, 'PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH'),
    ('PROJECT_REGISTER', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', 'OPTION', false, 2, 'PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE'),
    ('PROJECT_REPORT', 'PROJECT', 'المشروع', 'Project', 'PROJECT', true, 1, NULL, NULL),
    ('PROGRESS_REPORTING', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('PROGRESS_REPORTING', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', 'OPTION', false, 2, 'PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS'),
    ('PROGRESS_HISTORY', 'PROJECT', 'المشروع', 'Project', 'PROJECT', true, 1, NULL, NULL),
    ('SCHEDULE_DELIVERY', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني الحالية', 'Current schedule health', 'OPTION', false, 2, 'SCHEDULE_SCHEDULE_HEALTH_STATUS', 'SCHEDULE_HEALTH'),
    ('RISK_ISSUE', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('FINANCIAL_PERFORMANCE', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', 'OPTION', false, 2, 'FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION'),
    ('KPI_PERFORMANCE', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL),
    ('GOVERNANCE_CHANGE', 'DEPARTMENT', 'الإدارة', 'Department', 'DEPARTMENT', false, 1, NULL, NULL)
) AS v (code, parameter_code, label_ar, label_en, data_type, is_required, sort_order, source_entity_code, field_code)
JOIN reports.report_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
WHERE NOT EXISTS (SELECT 1 FROM reports.report_parameter p WHERE p.report_definition_id = d.id)
ON CONFLICT (report_definition_id, code) DO NOTHING;

-- The options of the OPTION parameters, each naming the catalogue entry it absorbs where it is one (ADR-006 MAPPED).
INSERT INTO reports.report_parameter_option (id, report_parameter_id, value_code, label_ar, label_en, catalogue_entry_reference,
                                             created_at, created_by, updated_at, updated_by)
SELECT md5('report_parameter_option:' || v.code || ':1:' || v.parameter_code || ':' || v.value_code)::uuid, p.id, v.value_code, v.label_ar, v.label_en,
       v.catalogue_entry_reference, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM (VALUES
    ('PORTFOLIO_SUMMARY', 'PUBLISHED_HEALTH', 'GREEN', 'أخضر', 'Green', NULL),
    ('PORTFOLIO_SUMMARY', 'PUBLISHED_HEALTH', 'AMBER', 'كهرماني', 'Amber', 'RPT-PRJ-004'),
    ('PORTFOLIO_SUMMARY', 'PUBLISHED_HEALTH', 'RED', 'أحمر', 'Red', 'RPT-PRJ-004'),
    ('PORTFOLIO_SUMMARY', 'PUBLISHED_HEALTH', 'UNKNOWN', 'غير معروف', 'Unknown', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'DRAFT', 'مسودة', 'Draft', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'SUBMITTED', 'مقدم', 'Submitted', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'UNDER_REVIEW', 'قيد المراجعة', 'Under review', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'RETURNED', 'معاد', 'Returned', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'APPROVED_PLANNED', 'معتمد ومخطط', 'Approved, planned', NULL),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'ACTIVE', 'نشط', 'Active', 'RPT-PRJ-003'),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'SUSPENDED', 'معلق', 'Suspended', 'RPT-SUS-001'),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'COMPLETED', 'مكتمل', 'Completed', 'RPT-CLO-001'),
    ('PROJECT_REGISTER', 'LIFECYCLE_STATE', 'CLOSED', 'مغلق', 'Closed', 'RPT-CLO-001'),
    ('PROGRESS_REPORTING', 'REPORTING_STATUS', 'UP_TO_DATE', 'محدث', 'Up to date', NULL),
    ('PROGRESS_REPORTING', 'REPORTING_STATUS', 'OVERDUE', 'متأخر', 'Overdue', 'RPT-PRG-001'),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_HEALTH', 'GREEN', 'أخضر', 'Green', NULL),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_HEALTH', 'AMBER', 'كهرماني', 'Amber', NULL),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_HEALTH', 'RED', 'أحمر', 'Red', NULL),
    ('SCHEDULE_DELIVERY', 'SCHEDULE_HEALTH', 'UNKNOWN', 'غير معروف', 'Unknown', NULL),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_CONDITION', 'GREEN', 'أخضر', 'Green', NULL),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_CONDITION', 'AMBER', 'كهرماني', 'Amber', NULL),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_CONDITION', 'RED', 'أحمر', 'Red', NULL),
    ('FINANCIAL_PERFORMANCE', 'FINANCIAL_CONDITION', 'UNKNOWN', 'غير معروف', 'Unknown', NULL)
) AS v (code, parameter_code, value_code, label_ar, label_en, catalogue_entry_reference)
JOIN reports.report_definition d ON d.code = v.code AND d.version_no = 1 AND d.created_by = '00000000-0000-4000-8000-0000000000ff'
JOIN reports.report_parameter p ON p.report_definition_id = d.id AND p.code = v.parameter_code
WHERE NOT EXISTS (SELECT 1 FROM reports.report_parameter_option o WHERE o.report_parameter_id = p.id)
ON CONFLICT (report_parameter_id, value_code) DO NOTHING;

-- 8. The SCR-138 explorer's allowlist (ADR-019; TASK-071): a DRAFT version 1 of REPORT_RULES holding the delivery team's proposal — every field of the
-- project's identity and of the register's project-grain projections, filterable, and sortable unless it is a reference. It stays DRAFT: the explorer
-- resolves PUBLISHED versions only and fails closed without one (TASK-034), so nothing is offered until AHDA's configuration administrators review,
-- narrow and publish it on FG-04 (reports.md F-5). No entry is classified: ADR-010's taxonomy is outstanding.
INSERT INTO master_data_config.configuration_version (id, configuration_family_id, version_no, lifecycle_state, change_summary, change_summary_lang, created_at, created_by, updated_at, updated_by)
SELECT md5('configuration_version:REPORT_RULES:1')::uuid, f.id, 1, 'DRAFT',
       'Proposed SCR-138 allowlist (delivery team, TASK-071): every project-grain field of the register. For AHDA to review, narrow and publish.', 'en',
       now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_family f
WHERE f.code = 'REPORT_RULES'
ON CONFLICT (configuration_family_id, version_no) DO NOTHING;

-- Entries are added to the seeded version only while it is DRAFT; once AHDA validates it, the seed leaves it alone.
INSERT INTO master_data_config.report_allowlist_entry (id, configuration_version_id, source_entity_code, field_code, label_ar, label_en, is_filterable, is_sortable,
                                                     created_at, created_by, updated_at, updated_by)
SELECT md5('report_allowlist_entry:REPORT_RULES:1:' || e.source_entity_code || '.' || e.field_code)::uuid, v.id, e.source_entity_code, e.field_code,
       e.label_ar, e.label_en, e.is_filterable, e.is_sortable, now(), '00000000-0000-4000-8000-0000000000ff', now(), '00000000-0000-4000-8000-0000000000ff'
FROM master_data_config.configuration_version v
CROSS JOIN (VALUES
    ('PROJECT', 'FORMAL_PROJECT_ID', 'رقم المشروع', 'Project ID', true, true),
    ('PROJECT', 'TITLE', 'اسم المشروع', 'Project', true, true),
    ('PROJECT', 'DEPARTMENT', 'الإدارة', 'Department', true, false),
    ('PROJECT', 'EXTERNAL_ENTITY', 'الجهة المنفذة', 'Delivering entity', true, false),
    ('PROJECT_LIFECYCLE_STATE', 'LIFECYCLE_STATE', 'حالة دورة الحياة', 'Lifecycle state', true, true),
    ('PROGRESS_PROJECT_HEALTH_STATUS', 'OVERALL_HEALTH', 'الصحة العامة الحالية', 'Current Overall Project Health', true, true),
    ('PROGRESS_PROJECT_HEALTH_STATUS', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية الحالية', 'Current actual progress %', true, true),
    ('PROGRESS_PROJECT_HEALTH_STATUS', 'PLANNED_PERCENT', 'نسبة الإنجاز المخططة الحالية', 'Current planned progress %', true, true),
    ('PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'OVERALL_HEALTH', 'الصحة العامة المنشورة', 'Published Overall Project Health', true, true),
    ('PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'ACTUAL_PERCENT', 'نسبة الإنجاز الفعلية المنشورة', 'Published actual progress %', true, true),
    ('PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'PLANNED_PERCENT', 'نسبة الإنجاز المخططة المنشورة', 'Published planned progress %', true, true),
    ('PROGRESS_PUBLISHED_PROGRESS_SNAPSHOT', 'ACTUAL_PERCENT_OVERRIDDEN', 'تم تجاوز نسبة الإنجاز', 'Actual progress overridden', true, true),
    ('PROGRESS_PUBLISHED_SCHEDULE_HEALTH', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني المنشورة', 'Published schedule health', true, true),
    ('PROGRESS_REPORTING_COMPLETENESS', 'REPORTING_STATUS', 'حالة تقارير التقدم', 'Progress reporting status', true, true),
    ('PROGRESS_REPORTING_COMPLETENESS', 'OVERDUE_PERIODS', 'الفترات المتأخرة', 'Overdue reporting periods', true, true),
    ('SCHEDULE_SCHEDULE_HEALTH_STATUS', 'SCHEDULE_HEALTH', 'صحة الجدول الزمني الحالية', 'Current schedule health', true, true),
    ('SCHEDULE_SCHEDULE_HEALTH_STATUS', 'FINISH_VARIANCE_DAYS', 'انحراف تاريخ الانتهاء بالأيام', 'Finish variance (days)', true, true),
    ('RISK_RISK_EXPOSURE', 'OPEN_RISKS', 'المخاطر المفتوحة', 'Open risks', true, true),
    ('RISK_RISK_EXPOSURE', 'NOT_ASSESSED', 'مخاطر غير مقيمة', 'Risks not assessed', true, true),
    ('RISK_RISK_EXPOSURE', 'REVIEW_OVERDUE', 'مراجعات مخاطر متأخرة', 'Risk reviews overdue', true, true),
    ('FINANCIAL_KPI_FINANCIAL_POSITION', 'FINANCIAL_CONDITION', 'الحالة المالية الحالية', 'Current financial condition', true, true),
    ('FINANCIAL_KPI_FINANCIAL_POSITION', 'APPROVED_BUDGET', 'الميزانية المعتمدة (حالي)', 'Approved budget (current)', true, true),
    ('FINANCIAL_KPI_FINANCIAL_POSITION', 'ACTUAL_EXPENDITURE_TO_DATE', 'الصرف الفعلي حتى تاريخه (حالي)', 'Actual expenditure to date (current)', true, true),
    ('FINANCIAL_KPI_FINANCIAL_POSITION', 'FORECAST_AT_COMPLETION', 'التكلفة المتوقعة عند الإنجاز (حالي)', 'Forecast at completion (current)', true, true),
    ('FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FINANCIAL_CONDITION', 'الحالة المالية المنشورة', 'Published financial condition', true, true),
    ('FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'APPROVED_BUDGET', 'الميزانية المعتمدة (منشور)', 'Approved budget (published)', true, true),
    ('FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'ACTUAL_EXPENDITURE_TO_DATE', 'الصرف الفعلي حتى تاريخه (منشور)', 'Actual expenditure to date (published)', true, true),
    ('FINANCIAL_KPI_PUBLISHED_FINANCIAL_SNAPSHOT', 'FORECAST_AT_COMPLETION', 'التكلفة المتوقعة عند الإنجاز (منشور)', 'Forecast at completion (published)', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'ACTIVE_ASSIGNMENTS', 'مؤشرات الأداء النشطة', 'Active KPIs', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'NOT_PUBLISHED', 'مؤشرات بلا قياس منشور', 'KPIs with no published measurement', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'RAG_GREEN', 'مؤشرات خضراء', 'KPIs green', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'RAG_AMBER', 'مؤشرات كهرمانية', 'KPIs amber', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'RAG_RED', 'مؤشرات حمراء', 'KPIs red', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'RAG_UNKNOWN', 'مؤشرات غير معروفة الحالة', 'KPIs unknown', true, true),
    ('FINANCIAL_KPI_KPI_CONDITION', 'RAG_NOT_APPLICABLE', 'مؤشرات لا تنطبق', 'KPIs not applicable', true, true),
    ('PROJECT_TASK_OPEN_TASKS', 'OPEN_TASKS', 'المهام المفتوحة', 'Open tasks', true, true),
    ('MILESTONE_OPEN_ACHIEVEMENT_CLAIMS', 'OPEN_ACHIEVEMENT_CLAIMS', 'مطالبات إنجاز معالم مفتوحة', 'Open milestone achievement claims', true, true),
    ('MANAGEMENT_CONCERN_OPEN_CONCERNS', 'OPEN_CONCERNS', 'القضايا والتحديات المفتوحة', 'Open issues and challenges', true, true),
    ('CHANGE_REQUEST_CHANGE_POSITION', 'UNDECIDED', 'طلبات تغيير قيد البت', 'Change requests undecided', true, true),
    ('CHANGE_REQUEST_CHANGE_POSITION', 'APPROVED_NOT_STARTED', 'طلبات تغيير معتمدة لم يبدأ تنفيذها', 'Change requests approved, not started', true, true),
    ('CHANGE_REQUEST_CHANGE_POSITION', 'IN_IMPLEMENTATION', 'طلبات تغيير قيد التنفيذ', 'Change requests in implementation', true, true),
    ('SUSPENSION_OPEN_REQUESTS', 'OPEN_REQUESTS', 'طلبات تعليق أو استئناف مفتوحة', 'Open suspension or resumption requests', true, true)
) AS e (source_entity_code, field_code, label_ar, label_en, is_filterable, is_sortable)
WHERE v.id = md5('configuration_version:REPORT_RULES:1')::uuid
  AND v.lifecycle_state = 'DRAFT'
ON CONFLICT (configuration_version_id, source_entity_code, field_code) DO NOTHING;
