using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The protected permission catalogue (ERD F-081) and the grants of the R01–R08 shipped-default profiles. db/seed
/// writes both to <c>identity_access</c>, and a seed test fails if the two disagree.
/// </summary>
/// <remarks>
/// The shipped grants are Blueprint v2.0 Appendix A as the 24 Oct 2026 prototype review applies it to the eight roles
/// (docs/architecture/shipped-permission-matrix.md). <see cref="GrantPolicy"/> bounds every grant, shipped or authored.
/// </remarks>
public sealed class PermissionCatalogue
{
    /// <summary>ADM-041 SSO/Directory Integration: R01 only (TASK-028).</summary>
    public const string IdentityIntegrationManage = "IDENTITY_INTEGRATION_MANAGE";

    /// <summary>ADM-002/003 User List and Detail (TASK-031).</summary>
    public const string UserView = "USER_VIEW";

    /// <summary>ADM-004/005 Create and Edit User; activate and disable (MOD-080) (TASK-031).</summary>
    public const string UserManage = "USER_MANAGE";

    /// <summary>ADM-006/007/009 Roles, the permission catalogue and the permission profiles with their grants (TASK-031).</summary>
    public const string RoleView = "ROLE_VIEW";

    /// <summary>ADM-008 Edit Role: the canonical role's bilingual name (TASK-031).</summary>
    public const string RoleManage = "ROLE_MANAGE";

    /// <summary>ADM-010 Role Assignment and MOD-081: bind a user to a published profile version (ADR-018, TASK-031).</summary>
    public const string RoleAssign = "ROLE_ASSIGN";

    /// <summary>ADM-011–013 Organization Structure, Departments and Entities (TASK-031).</summary>
    public const string OrganizationView = "ORGANIZATION_VIEW";

    /// <summary>ADM-011–013: create, edit, activate and deactivate departments and entities (TASK-031).</summary>
    public const string OrganizationManage = "ORGANIZATION_MANAGE";

    /// <summary>ADM-020–029 master data catalogues and items, and the KPI catalogue (TASK-034).</summary>
    public const string MasterDataView = "MASTER_DATA_VIEW";

    /// <summary>ADM-020–029: author, validate, publish and retire master data items and KPI definitions; rename a catalogue (TASK-034).</summary>
    public const string MasterDataManage = "MASTER_DATA_MANAGE";

    /// <summary>FG-04 configuration families, versions, their content, and resolution as of a date (TASK-034).</summary>
    public const string ConfigurationView = "CONFIGURATION_VIEW";

    /// <summary>FG-04: author, validate, publish and retire configuration versions (TASK-034).</summary>
    public const string ConfigurationManage = "CONFIGURATION_MANAGE";

    /// <summary>WF-11 approval runs and their history (SCR-101, SCR-115), on the records the grant's scope covers (TASK-035).</summary>
    public const string ApprovalView = "APPROVAL_VIEW";

    /// <summary>
    /// WF-11: decide an approval task (SCR-100) and delegate that authority (SCR-114). Held through the role an
    /// APPROVAL_AUTHORITY stage names, with a scope covering the run's anchors (TASK-035). No role holds it until
    /// Blueprint Appendix A grants it (record F-1).
    /// </summary>
    public const string ApprovalDecide = "APPROVAL_DECIDE";

    /// <summary>
    /// WF-12: find and read documents, their version history, links and evidence references, and download CLEAN content
    /// (SCR-120–124), on the documents the grant's scope and clearance cover (TASK-037).
    /// </summary>
    public const string DocumentView = "DOCUMENT_VIEW";

    /// <summary>WF-12: upload a document and add a version to one (MOD-050, MOD-052) (TASK-037).</summary>
    public const string DocumentUpload = "DOCUMENT_UPLOAD";

    /// <summary>WF-12: edit a document's metadata, archive it, queue a failed scan again (MOD-051, MOD-053) (TASK-037).</summary>
    public const string DocumentManage = "DOCUMENT_MANAGE";

    /// <summary>WF-01: SCR-025 Project Register and SCR-026 Project Detail, on the projects the grant's scope covers (TASK-041).</summary>
    public const string ProjectView = "PROJECT_VIEW";

    /// <summary>
    /// WF-01 registration (TASK-041): create a project draft, edit a DRAFT or RETURNED project, submit it, withdraw a
    /// submission, delete one's own DRAFT. ADR-013: an entity user may register a project of their own entity.
    /// </summary>
    public const string ProjectRegister = "PROJECT_REGISTER";

    /// <summary>WF-01: start AHDA's review of a SUBMITTED project, which starts its WF-11 run (TASK-041). Internal users only (ADR-013).</summary>
    public const string ProjectReview = "PROJECT_REVIEW";

    /// <summary>WF-01: the Planned → Active command (TASK-041). Internal users only (ADR-013).</summary>
    public const string ProjectActivate = "PROJECT_ACTIVATE";

    /// <summary>
    /// WF-02: a project's reporting periods, progress submissions, published snapshots and Overall Project Health (SCR-048,
    /// SCR-070), on the projects the grant's scope covers (TASK-044).
    /// </summary>
    public const string ProgressView = "PROGRESS_VIEW";

    /// <summary>
    /// WF-02: start a period's progress update, edit its narrative and override, submit it (TASK-044). ADR-013: an assigned
    /// entity Project Manager submits progress on their own project.
    /// </summary>
    public const string ProgressSubmit = "PROGRESS_SUBMIT";

    /// <summary>WF-02: review a submission, return it or publish it (TASK-044). Internal users only, and never one's own submission (ADR-013).</summary>
    public const string ProgressReview = "PROGRESS_REVIEW";

    /// <summary>
    /// WF-03: a project's schedule, activities, dependencies, baselines, variance and Schedule Health (SCR-044, SCR-045,
    /// SCR-061), on the projects the grant's scope covers (TASK-046).
    /// </summary>
    public const string ScheduleView = "SCHEDULE_VIEW";

    /// <summary>
    /// WF-03: build the schedule, maintain its forecast, prepare and submit baseline candidates (TASK-046). Approval is WF-11's
    /// APPROVAL_DECIDE, not this permission.
    /// </summary>
    public const string ScheduleEdit = "SCHEDULE_EDIT";

    /// <summary>
    /// WF-04: a project's tasks, subtasks, task dependencies and Activity Execution Progress (SCR-047, SCR-063–066), on the
    /// tasks the grant's scope covers: OWN reaches the projects the holder manages, ASSIGNED the tasks assigned to the holder
    /// (TASK-048).
    /// </summary>
    public const string TaskView = "TASK_VIEW";

    /// <summary>
    /// WF-04 execution (TASK-048): start, block, unblock and complete a task, and maintain a leaf's actual percentage. ADR-009:
    /// the task owner maintains it and the Project Manager may edit it; ADR-013: an assigned entity Project Manager acts on the
    /// tasks of their own project.
    /// </summary>
    public const string TaskUpdate = "TASK_UPDATE";

    /// <summary>WF-04 planning (TASK-048): create and edit tasks and subtasks, assign them, set their dependencies, cancel a task.</summary>
    public const string TaskManage = "TASK_MANAGE";

    /// <summary>WF-04 (TASK-048): reopen a COMPLETED task. A permission of its own, so neither TASK_UPDATE nor TASK_MANAGE reopens one.</summary>
    public const string TaskReopen = "TASK_REOPEN";

    /// <summary>
    /// WF-05: the achievement revisions of a project's milestones and their evidence (TASK-050), on the projects the grant's
    /// scope covers.
    /// </summary>
    public const string MilestoneView = "MILESTONE_VIEW";

    /// <summary>
    /// WF-05 (TASK-050): claim a milestone's achievement — open a revision, edit it while DRAFT, attach its evidence, submit
    /// it to WF-11 — and correct an accepted one with a new revision. ADR-013: an entity Project Manager may submit claims on
    /// their own project; acceptance stays with WF-05, decided through WF-11's APPROVAL_DECIDE, never by this permission.
    /// </summary>
    public const string MilestoneSubmit = "MILESTONE_SUBMIT";

    /// <summary>WF-06: a project's risk register, assessment versions, treatment actions, acceptances and the issues raised from its risks (TASK-055).</summary>
    public const string RiskView = "RISK_VIEW";

    /// <summary>
    /// WF-06 (TASK-055): register and edit risks, assign their owners, plan and progress their treatment actions, move them to
    /// treatment or monitoring, close them, and raise an issue from one. ADR-013: an entity Project Manager registers and updates
    /// risks on their own project.
    /// </summary>
    public const string RiskManage = "RISK_MANAGE";

    /// <summary>WF-06 (TASK-055): record an assessment, which rates the risk. Internal users only: rating authority is unchanged by ADR-013.</summary>
    public const string RiskAssess = "RISK_ASSESS";

    /// <summary>WF-06 (TASK-055): accept a risk until an expiry, and revoke an acceptance. Internal users only: acceptance authority is unchanged by ADR-013.</summary>
    public const string RiskAccept = "RISK_ACCEPT";

    /// <summary>WF-06 (TASK-055): reopen a CLOSED risk. A permission of its own, so RISK_MANAGE never reopens one.</summary>
    public const string RiskReopen = "RISK_REOPEN";

    /// <summary>WF-07 (TASK-057): a project's issues and challenges, their impacts, computed severity, status and escalations.</summary>
    public const string ConcernView = "CONCERN_VIEW";

    /// <summary>WF-07 (TASK-057): raise an issue or a challenge. ADR-013: an entity raises a blocker or an issue on its own project.</summary>
    public const string ConcernRaise = "CONCERN_RAISE";

    /// <summary>
    /// WF-07 (TASK-057): edit, assess, assign, progress, review and close concerns, and submit a resolution for validation. Internal
    /// users only: ADR-013 gives entities intake and visibility, not management.
    /// </summary>
    public const string ConcernManage = "CONCERN_MANAGE";

    /// <summary>WF-07 (TASK-057): escalate a concern, and withdraw one's own escalation. Internal users only: ADR-013, "not escalation".</summary>
    public const string ConcernEscalate = "CONCERN_ESCALATE";

    /// <summary>WF-07 (TASK-057): resolve an escalation, held through the role it is addressed to. Internal users only.</summary>
    public const string ConcernEscalationResolve = "CONCERN_ESCALATION_RESOLVE";

    /// <summary>WF-08 (TASK-060): a project's change requests, their materiality evaluations and the authorisations their approval issued.</summary>
    public const string ChangeRequestView = "CHANGE_REQUEST_VIEW";

    /// <summary>
    /// WF-08 (TASK-060): raise a change request, edit and delete its draft, submit it and withdraw it. ADR-013: an entity Project
    /// Manager may raise a change request on their own project.
    /// </summary>
    public const string ChangeRequestRaise = "CHANGE_REQUEST_RAISE";

    /// <summary>
    /// WF-08 (TASK-060): start the review of a submitted change request, which records its materiality and routes it through WF-11.
    /// Internal users only: materiality and approval remain AHDA's (ADR-013).
    /// </summary>
    public const string ChangeRequestReview = "CHANGE_REQUEST_REVIEW";

    /// <summary>
    /// WF-08 (TASK-060): start the implementation of an approved change request, mark it implemented and close it. Internal users only:
    /// implementation remains AHDA's (ADR-013).
    /// </summary>
    public const string ChangeRequestImplement = "CHANGE_REQUEST_IMPLEMENT";

    /// <summary>WF-09 (TASK-062): a project's suspension and resumption requests and its suspension periods, open and ended.</summary>
    public const string SuspensionView = "SUSPENSION_VIEW";

    /// <summary>
    /// WF-09 (TASK-062): raise a suspension or resumption request, edit and delete its draft, submit it and withdraw it. ADR-013: an
    /// entity Project Manager fills in the request on their own project; AHDA decides it.
    /// </summary>
    public const string SuspensionRaise = "SUSPENSION_RAISE";

    /// <summary>WF-09 (TASK-062): start the review of a submitted request, which routes it through WF-11. Internal users only (ADR-013).</summary>
    public const string SuspensionReview = "SUSPENSION_REVIEW";

    /// <summary>
    /// WF-09 (TASK-062): activate an approved request whose effective date has come — the project's lifecycle transition, apart from the
    /// approval. Internal users only (ADR-013). WF-09's own pass activates due requests as its service principal without it.
    /// </summary>
    public const string SuspensionActivate = "SUSPENSION_ACTIVATE";

    /// <summary>WF-10 (TASK-063): a project's completion and closure cases with their readiness, and its post-project obligations.</summary>
    public const string CloseoutView = "CLOSEOUT_VIEW";

    /// <summary>
    /// WF-10 (TASK-063): raise a completion or closure case, edit and delete its draft, evaluate its readiness, submit and withdraw it, and
    /// record and keep the project's post-project obligations. ADR-013: an entity Project Manager fills in the case on their own project;
    /// AHDA decides it.
    /// </summary>
    public const string CloseoutRaise = "CLOSEOUT_RAISE";

    /// <summary>WF-10 (TASK-063): start the review of a submitted case, which routes it through WF-11. Internal users only (ADR-013).</summary>
    public const string CloseoutReview = "CLOSEOUT_REVIEW";

    /// <summary>
    /// WF-10 (TASK-063): accept a failed readiness criterion as an exception (READY_WITH_CONDITIONS), and waive a post-project obligation.
    /// Internal users only (ADR-013).
    /// </summary>
    public const string CloseoutWaive = "CLOSEOUT_WAIVE";

    /// <summary>
    /// WF-10 (TASK-063): activate an approved case — the project's lifecycle transition, apart from the approval. Internal users only
    /// (ADR-013). WF-10's own pass activates approved cases as its service principal without it.
    /// </summary>
    public const string CloseoutActivate = "CLOSEOUT_ACTIVATE";

    /// <summary>
    /// WF-13 (TASK-066): external update requests and their contribution revisions — AHDA's full view internally, and an external caller's
    /// least-disclosure projection of its own entity's issued requests (WF-13 §8.2).
    /// </summary>
    public const string ExternalRequestView = "EXTERNAL_REQUEST_VIEW";

    /// <summary>
    /// WF-13 (TASK-066): draft, edit, delete, issue and cancel an update request, and name its responder and reviewer. Internal users only:
    /// AHDA issues update requests (TASK-066 gate decision).
    /// </summary>
    public const string ExternalRequestManage = "EXTERNAL_REQUEST_MANAGE";

    /// <summary>WF-13 (TASK-066): draft and submit the answer to a request the holder is the named responder of (EXT-CC-04).</summary>
    public const string ExternalContributionRespond = "EXTERNAL_CONTRIBUTION_RESPOND";

    /// <summary>
    /// WF-13 (TASK-066): start the review of a submitted contribution revision and accept, return or reject it, as the request's assigned
    /// reviewer. Internal users only; never an edit of the submitted values (EXT-P-10).
    /// </summary>
    public const string ExternalContributionReview = "EXTERNAL_CONTRIBUTION_REVIEW";

    /// <summary>WF-13 (TASK-066): apply an accepted contribution to its source record through its typed adapter, and revalidate a conflict. Internal users only.</summary>
    public const string ExternalContributionApply = "EXTERNAL_CONTRIBUTION_APPLY";

    /// <summary>
    /// WF-14 Financial Progress (TASK-052): a project's Approved Budget versions, financial updates, published financial
    /// snapshots, live position, source modes and portfolio totals. ADR-013: an entity sees budget and expenditure for its own
    /// project, masked by audience (ADR-010).
    /// </summary>
    public const string FinancialView = "FINANCIAL_VIEW";

    /// <summary>
    /// WF-14 (ADR-010): the SAR amounts of the financial records <see cref="FinancialView"/> reaches — budget, expenditure, forecast — on the
    /// records the grant's scope covers. Without it a holder of <see cref="FinancialView"/> sees the financial condition and its dates, and
    /// every amount is withheld (shipped-permission-matrix.md D-4). Never held by an external user (<see cref="GrantPolicy"/>).
    /// </summary>
    public const string FinancialViewSensitive = "FINANCIAL_VIEW_SENSITIVE";

    /// <summary>WF-14 (TASK-052): open and edit an Approved Budget version, attach its referenced document, submit it to WF-11; enter and submit a period's actuals and forecast.</summary>
    public const string FinancialSubmit = "FINANCIAL_SUBMIT";

    /// <summary>WF-14 (TASK-052): review a period's financial update, return it or publish it. Internal users only, and never one's own submission (ADR-013).</summary>
    public const string FinancialReview = "FINANCIAL_REVIEW";

    /// <summary>WF-14 (TASK-052, ADR-008): set a financial field's source mode — MANUAL, INTEGRATED or HYBRID — per project.</summary>
    public const string FinancialSourceManage = "FINANCIAL_SOURCE_MANAGE";

    /// <summary>
    /// WF-14 KPI Performance (TASK-052): a project's KPI assignments, target versions, measurements and portfolio aggregates.
    /// ADR-013: an entity sees KPI status for its own project, masked by audience (ADR-010).
    /// </summary>
    public const string KpiView = "KPI_VIEW";

    /// <summary>WF-14 (TASK-052): assign a KPI to a project, suspend or retire it, and open and submit its target versions to WF-11.</summary>
    public const string KpiManage = "KPI_MANAGE";

    /// <summary>WF-14 (TASK-052): record, edit and submit a KPI measurement.</summary>
    public const string KpiRecord = "KPI_RECORD";

    /// <summary>WF-14 (TASK-052): publish a submitted measurement. Internal users only, and never the person who recorded it (ADR-013).</summary>
    public const string KpiReview = "KPI_REVIEW";

    /// <summary>WF-15: the bilingual notification templates and their versions (TASK-039).</summary>
    public const string NotificationTemplateView = "NOTIFICATION_TEMPLATE_VIEW";

    /// <summary>WF-15: author, validate, publish and retire notification templates (TASK-039).</summary>
    public const string NotificationTemplateManage = "NOTIFICATION_TEMPLATE_MANAGE";

    /// <summary>WF-15 operations: received intents, their deliveries, retries, suppressions and dead letters (TASK-039).</summary>
    public const string NotificationDeliveryView = "NOTIFICATION_DELIVERY_VIEW";

    /// <summary>WF-15 operations: redrive a failed intent or a dead-lettered delivery (TASK-039).</summary>
    public const string NotificationDeliveryManage = "NOTIFICATION_DELIVERY_MANAGE";

    /// <summary>ADR-019: personalise one's own dashboard layout (TASK-111).</summary>
    public const string LayoutPersonalize = "LAYOUT_PERSONALIZE";

    /// <summary>ADR-019: compose a report in the controlled report explorer (TASK-112).</summary>
    public const string ReportCompose = "REPORT_COMPOSE";

    /// <summary>
    /// FG-02 (TASK-071): generate a report's PDF, XLSX or CSV and download it. View never implies export (BR-RPT-013): a report job's rows are the
    /// projects the holder reaches under this permission as well as under each projection's own.
    /// </summary>
    public const string ReportExport = "REPORT_EXPORT";

    /// <summary>
    /// FG-06 (TASK-073): a project's business Activity timeline (SCR-057, AUD.ACTIVITY.VIEW), on the projects the grant's scope covers.
    /// It shows safe Activity lines, never formal audit evidence.
    /// </summary>
    public const string ActivityView = "ACTIVITY_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of authentication and authorization refusals (ADM-050/051, AUD.SECURITY.VIEW).</summary>
    public const string AuditSecurityView = "AUDIT_SECURITY_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of privileged actions, permission changes and configuration changes (AUD.ADMIN.VIEW).</summary>
    public const string AuditAdministrationView = "AUDIT_ADMINISTRATION_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of material integration operator actions (ADM-053, AUD.INTEGRATION.VIEW).</summary>
    public const string AuditIntegrationView = "AUDIT_INTEGRATION_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of WF-14's financial and KPI events (AUD.FINANCIAL.VIEW).</summary>
    public const string AuditFinancialView = "AUDIT_FINANCIAL_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of WF-13's external participation events (AUD.EXTERNAL.VIEW). Never held by an external user's need to know.</summary>
    public const string AuditExternalView = "AUDIT_EXTERNAL_VIEW";

    /// <summary>FG-06 (TASK-073): formal audit of every other lifecycle transition, data change and approval decision (SCR-058, ADM-050).</summary>
    public const string AuditBusinessView = "AUDIT_BUSINESS_VIEW";

    /// <summary>FG-06 (TASK-073): an audit event's request context — the client address and the name typed at sign-in (AUD.CONTEXT.SENSITIVE).</summary>
    public const string AuditContextView = "AUDIT_CONTEXT_VIEW";

    /// <summary>
    /// FG-06 (TASK-073): operate the audit store — ingestion status, integrity verification, and checking and rebuilding the Activity
    /// projection (AUD-API-035, -037, -039; AUD.HEALTH.VIEW). It reveals no event's content.
    /// </summary>
    public const string AuditOperationsManage = "AUDIT_OPERATIONS_MANAGE";

    /// <summary>
    /// FG-05 (TASK-075): ADM-040, -046, -047 — the integration catalogue, each instance's availability and freshness, its measures, its runs and
    /// its operational alerts (INT.VIEW, INT.VIEW_HEALTH, INT.VIEW_ALERTS). Platform-wide: there is no record to scope.
    /// </summary>
    public const string IntegrationView = "INTEGRATION_VIEW";

    /// <summary>FG-05 (TASK-075): ADM-048 — attempts and their lineages, searched by correlation id (INT.VIEW_LOGS, INT.SEARCH_CORRELATION). No payload exists to show.</summary>
    public const string IntegrationLogView = "INTEGRATION_LOG_VIEW";

    /// <summary>FG-05 (TASK-075): retry a lineage FG-05 runs, and request a reconciliation (INT.RETRY, INT.RUN_RECONCILIATION). Confers no business authority (BR-INT-021).</summary>
    public const string IntegrationOperate = "INTEGRATION_OPERATE";

    /// <summary>FG-05 (TASK-075): acknowledge, resolve and dismiss operational alerts (INT.ACK_ALERT, INT.RESOLVE_ALERT).</summary>
    public const string IntegrationAlertManage = "INTEGRATION_ALERT_MANAGE";

    /// <summary>FG-05 (TASK-075): enable and disable an integration's monitoring (MOD-101), apart from operating it (FG-05 §12.3).</summary>
    public const string IntegrationConfigure = "INTEGRATION_CONFIGURE";

    /// <summary>
    /// TASK-102: load a register's migration workbook — validate it, and import it in the legacy state each row records, around the
    /// registers' approval workflows. Platform-wide: there is no record to scope.
    /// </summary>
    public const string DataImport = "DATA_IMPORT";

    public static PermissionCatalogue Platform { get; } = new(
        [
            new(IdentityIntegrationManage, "IDENTITY_ACCESS", AccessMode.Write),
            new(UserView, "IDENTITY_ACCESS", AccessMode.Read),
            new(UserManage, "IDENTITY_ACCESS", AccessMode.Write),
            new(RoleView, "IDENTITY_ACCESS", AccessMode.Read),
            new(RoleManage, "IDENTITY_ACCESS", AccessMode.Write),
            new(RoleAssign, "IDENTITY_ACCESS", AccessMode.Write),
            new(OrganizationView, "IDENTITY_ACCESS", AccessMode.Read),
            new(OrganizationManage, "IDENTITY_ACCESS", AccessMode.Write),
            new(MasterDataView, "MASTER_DATA_CONFIG", AccessMode.Read),
            new(MasterDataManage, "MASTER_DATA_CONFIG", AccessMode.Write),
            new(ConfigurationView, "MASTER_DATA_CONFIG", AccessMode.Read),
            new(ConfigurationManage, "MASTER_DATA_CONFIG", AccessMode.Write),
            new(ApprovalView, "APPROVAL", AccessMode.Read),
            new(ApprovalDecide, "APPROVAL", AccessMode.Write),
            new(DocumentView, "DOCUMENT_MANAGEMENT", AccessMode.Read),
            new(DocumentUpload, "DOCUMENT_MANAGEMENT", AccessMode.Write),
            new(DocumentManage, "DOCUMENT_MANAGEMENT", AccessMode.Write),
            new(ProjectView, "PROJECT", AccessMode.Read),
            new(ProjectRegister, "PROJECT", AccessMode.Write),
            new(ProjectReview, "PROJECT", AccessMode.Write),
            new(ProjectActivate, "PROJECT", AccessMode.Write),
            new(ProgressView, "PROGRESS", AccessMode.Read),
            new(ProgressSubmit, "PROGRESS", AccessMode.Write),
            new(ProgressReview, "PROGRESS", AccessMode.Write),
            new(ScheduleView, "SCHEDULE", AccessMode.Read),
            new(ScheduleEdit, "SCHEDULE", AccessMode.Write),
            new(TaskView, "PROJECT_TASK", AccessMode.Read),
            new(TaskUpdate, "PROJECT_TASK", AccessMode.Write),
            new(TaskManage, "PROJECT_TASK", AccessMode.Write),
            new(TaskReopen, "PROJECT_TASK", AccessMode.Write),
            new(MilestoneView, "MILESTONE", AccessMode.Read),
            new(MilestoneSubmit, "MILESTONE", AccessMode.Write),
            new(RiskView, "RISK", AccessMode.Read),
            new(RiskManage, "RISK", AccessMode.Write),
            new(RiskAssess, "RISK", AccessMode.Write),
            new(RiskAccept, "RISK", AccessMode.Write),
            new(RiskReopen, "RISK", AccessMode.Write),
            new(ConcernView, "MANAGEMENT_CONCERN", AccessMode.Read),
            new(ConcernRaise, "MANAGEMENT_CONCERN", AccessMode.Write),
            new(ConcernManage, "MANAGEMENT_CONCERN", AccessMode.Write),
            new(ConcernEscalate, "MANAGEMENT_CONCERN", AccessMode.Write),
            new(ConcernEscalationResolve, "MANAGEMENT_CONCERN", AccessMode.Write),
            new(ChangeRequestView, "CHANGE_REQUEST", AccessMode.Read),
            new(ChangeRequestRaise, "CHANGE_REQUEST", AccessMode.Write),
            new(ChangeRequestReview, "CHANGE_REQUEST", AccessMode.Write),
            new(ChangeRequestImplement, "CHANGE_REQUEST", AccessMode.Write),
            new(SuspensionView, "SUSPENSION", AccessMode.Read),
            new(SuspensionRaise, "SUSPENSION", AccessMode.Write),
            new(SuspensionReview, "SUSPENSION", AccessMode.Write),
            new(SuspensionActivate, "SUSPENSION", AccessMode.Write),
            new(CloseoutView, "CLOSEOUT", AccessMode.Read),
            new(CloseoutRaise, "CLOSEOUT", AccessMode.Write),
            new(CloseoutReview, "CLOSEOUT", AccessMode.Write),
            new(CloseoutWaive, "CLOSEOUT", AccessMode.Write),
            new(CloseoutActivate, "CLOSEOUT", AccessMode.Write),
            new(ExternalRequestView, "EXTERNAL_PARTICIPATION", AccessMode.Read),
            new(ExternalRequestManage, "EXTERNAL_PARTICIPATION", AccessMode.Write),
            new(ExternalContributionRespond, "EXTERNAL_PARTICIPATION", AccessMode.Write),
            new(ExternalContributionReview, "EXTERNAL_PARTICIPATION", AccessMode.Write),
            new(ExternalContributionApply, "EXTERNAL_PARTICIPATION", AccessMode.Write),
            new(FinancialView, "FINANCIAL", AccessMode.Read),
            new(FinancialViewSensitive, "FINANCIAL", AccessMode.Read),
            new(FinancialSubmit, "FINANCIAL", AccessMode.Write),
            new(FinancialReview, "FINANCIAL", AccessMode.Write),
            new(FinancialSourceManage, "FINANCIAL", AccessMode.Write),
            new(KpiView, "KPI", AccessMode.Read),
            new(KpiManage, "KPI", AccessMode.Write),
            new(KpiRecord, "KPI", AccessMode.Write),
            new(KpiReview, "KPI", AccessMode.Write),
            new(NotificationTemplateView, "NOTIFICATIONS", AccessMode.Read),
            new(NotificationTemplateManage, "NOTIFICATIONS", AccessMode.Write),
            new(NotificationDeliveryView, "NOTIFICATIONS", AccessMode.Read),
            new(NotificationDeliveryManage, "NOTIFICATIONS", AccessMode.Write),
            new(LayoutPersonalize, "DASHBOARDS", AccessMode.Write),
            new(ReportCompose, "REPORTS", AccessMode.Write),
            new(ReportExport, "REPORTS", AccessMode.Read),
            new(ActivityView, "ACTIVITY", AccessMode.Read),
            new(AuditSecurityView, "AUDIT_SECURITY", AccessMode.Read),
            new(AuditAdministrationView, "AUDIT_ADMINISTRATION", AccessMode.Read),
            new(AuditIntegrationView, "AUDIT_INTEGRATION", AccessMode.Read),
            new(AuditFinancialView, "AUDIT_FINANCIAL", AccessMode.Read),
            new(AuditExternalView, "AUDIT_EXTERNAL", AccessMode.Read),
            new(AuditBusinessView, "AUDIT_BUSINESS", AccessMode.Read),
            new(AuditContextView, "AUDIT_CONTEXT", AccessMode.Read),
            new(AuditOperationsManage, "AUDIT_OPERATIONS", AccessMode.Write),
            new(IntegrationView, "INTEGRATION_MONITORING", AccessMode.Read),
            new(IntegrationLogView, "INTEGRATION_MONITORING", AccessMode.Read),
            new(IntegrationOperate, "INTEGRATION_MONITORING", AccessMode.Write),
            new(IntegrationAlertManage, "INTEGRATION_MONITORING", AccessMode.Write),
            new(IntegrationConfigure, "INTEGRATION_MONITORING", AccessMode.Write),
            new(DataImport, "DATA_IMPORT", AccessMode.Write),
        ]);

    /// <summary>
    /// What the Portfolio and Department Managers hold, at the role's scope: Appendix A's "V/C/E/S/A*" over projects, schedules, progress,
    /// risks, requests and documents, the WF-11 inbox (C1) and the business audit trail (H4). Master data, configuration, layout and
    /// composition are added at their own scopes.
    /// </summary>
    private static readonly string[] ManagerPermissions =
    [
        ApprovalView, ApprovalDecide, ReportExport, ProjectView, ProjectRegister, ProjectReview, ProjectActivate, ProgressView, ProgressReview,
        ScheduleView, TaskView, MilestoneView, RiskView, RiskManage, RiskAssess, RiskAccept, RiskReopen,
        ConcernView, ConcernRaise, ConcernEscalationResolve, ChangeRequestView, ChangeRequestRaise, ChangeRequestReview,
        SuspensionView, SuspensionRaise, SuspensionReview, CloseoutView, CloseoutRaise, CloseoutReview, CloseoutWaive,
        ExternalRequestView, ExternalRequestManage, ExternalContributionReview,
        FinancialView, FinancialViewSensitive, FinancialReview, KpiView, KpiManage, KpiReview,
        DocumentView, DocumentUpload, DocumentManage, ActivityView, AuditBusinessView,
    ];

    /// <summary>
    /// The grants of the R01–R08 shipped-default profiles: Blueprint v2.0 Appendix A as the 24 Oct 2026 prototype review applies it, each
    /// role's scope the review's (C1, C2, H3, H4) — R02 and R07 ALL, R03 and R06 DEPT, R04 OWN, R05 ASSIGNED, R08 ENTITY — and every row's
    /// source and every departure from Appendix A in docs/architecture/shipped-permission-matrix.md. R01 administers and reads no business
    /// data (Appendix A.1). Approval and review authority goes to the Portfolio and Department Managers, decisions to the Executive too; the
    /// Project Manager prepares and submits. Master data and configuration are platform-wide, so their views are ALL; Personalize Layout and
    /// Compose Report act on the holder's own layout or definition, so OWN (ADR-019). Activation of approved suspensions and closures is the
    /// workflows' own service and ships to no role. <see cref="GrantPolicy"/> bounds every grant whatever a profile holds.
    /// </summary>
    public static IReadOnlyList<ShippedGrant> ShippedDefaultGrants { get; } =
    [
        // R01 System Administrator: administration, configuration, operations; no business data, no approval (Appendix A.1).
        new("R01", IdentityIntegrationManage, DataScope.All),
        new("R01", UserView, DataScope.All),
        new("R01", UserManage, DataScope.All),
        new("R01", RoleView, DataScope.All),
        new("R01", RoleManage, DataScope.All),
        new("R01", RoleAssign, DataScope.All),
        new("R01", OrganizationView, DataScope.All),
        new("R01", OrganizationManage, DataScope.All),
        new("R01", MasterDataView, DataScope.All),
        new("R01", MasterDataManage, DataScope.All),
        new("R01", ConfigurationView, DataScope.All),
        new("R01", ConfigurationManage, DataScope.All),
        new("R01", NotificationTemplateView, DataScope.All),
        new("R01", NotificationTemplateManage, DataScope.All),
        new("R01", NotificationDeliveryView, DataScope.All),
        new("R01", NotificationDeliveryManage, DataScope.All),
        new("R01", AuditAdministrationView, DataScope.All),
        new("R01", AuditOperationsManage, DataScope.All),
        new("R01", IntegrationView, DataScope.All),
        new("R01", IntegrationLogView, DataScope.All),
        new("R01", IntegrationOperate, DataScope.All),
        new("R01", IntegrationAlertManage, DataScope.All),
        new("R01", IntegrationConfigure, DataScope.All),
        new("R01", DataImport, DataScope.All),

        // R02 Portfolio Manager: the whole portfolio; registers, reviews, approves and activates.
        new("R02", MasterDataView, DataScope.All),
        new("R02", ConfigurationView, DataScope.All),
        new("R02", LayoutPersonalize, DataScope.Own),
        new("R02", ReportCompose, DataScope.Own),
        .. Role("R02", DataScope.All, ManagerPermissions),

        // R03 Department Manager: the same authority over its own department.
        new("R03", MasterDataView, DataScope.All),
        new("R03", ConfigurationView, DataScope.All),
        new("R03", LayoutPersonalize, DataScope.Own),
        new("R03", ReportCompose, DataScope.Own),
        .. Role("R03", DataScope.Dept, ManagerPermissions),

        // R04 Project Manager: the projects it manages — prepares, submits and runs them.
        new("R04", MasterDataView, DataScope.All),
        .. Role("R04", DataScope.Own,
        [
            ApprovalView, ReportExport, ProjectView, ProjectRegister, ProgressView, ProgressSubmit, ScheduleView, ScheduleEdit,
            TaskView, TaskUpdate, TaskManage, TaskReopen, MilestoneView, MilestoneSubmit, RiskView, RiskManage,
            ConcernView, ConcernRaise, ConcernManage, ConcernEscalate, ChangeRequestView, ChangeRequestRaise, ChangeRequestImplement,
            SuspensionView, SuspensionRaise, CloseoutView, CloseoutRaise,
            ExternalRequestView, ExternalRequestManage, ExternalContributionReview, ExternalContributionApply,
            FinancialView, FinancialViewSensitive, FinancialSubmit, KpiView, KpiManage, KpiRecord,
            DocumentView, DocumentUpload, ActivityView, AuditBusinessView,
        ]),

        // R05 Liaison: the projects it is assigned to — issues and monitors external update requests; no financials, no approvals.
        new("R05", MasterDataView, DataScope.All),
        .. Role("R05", DataScope.Assigned,
        [
            ReportExport, ProjectView, ProgressView, ScheduleView, TaskView, MilestoneView, RiskView, ConcernView, SuspensionView, CloseoutView,
            ExternalRequestView, ExternalRequestManage, ExternalContributionReview, KpiView, DocumentView, DocumentUpload, ActivityView, AuditBusinessView,
        ]),

        // R06 Viewer: its department, read only, amounts withheld.
        new("R06", MasterDataView, DataScope.All),
        .. Role("R06", DataScope.Dept,
        [
            ProjectView, ProgressView, ScheduleView, TaskView, MilestoneView, RiskView, ConcernView, ChangeRequestView, SuspensionView, CloseoutView,
            FinancialView, KpiView, DocumentView, AuditBusinessView,
        ]),

        // R07 Executive: the whole portfolio, read and decide; no operational editing.
        new("R07", MasterDataView, DataScope.All),
        new("R07", ConfigurationView, DataScope.All),
        new("R07", LayoutPersonalize, DataScope.Own),
        new("R07", ReportCompose, DataScope.Own),
        .. Role("R07", DataScope.All,
        [
            ApprovalView, ApprovalDecide, ReportExport, ProjectView, ProgressView, ScheduleView, TaskView, MilestoneView, RiskView,
            ConcernView, ConcernEscalationResolve, ChangeRequestView, SuspensionView, CloseoutView,
            FinancialView, FinancialViewSensitive, KpiView, DocumentView, ActivityView, AuditBusinessView,
        ]),

        // R08 External Entity User: its own entity's projects (ADR-013), the Project Dashboard and no reports (ADR-019), amounts withheld.
        new("R08", MasterDataView, DataScope.All),
        .. Role("R08", DataScope.Entity,
        [
            ProjectView, ProjectRegister, ProgressView, RiskView, ConcernView, ConcernRaise, ExternalRequestView, FinancialView, KpiView, ActivityView,
        ]),
        new("R08", TaskView, DataScope.Assigned),
        new("R08", TaskUpdate, DataScope.Assigned),
        new("R08", ExternalContributionRespond, DataScope.Assigned),
    ];

    private static IEnumerable<ShippedGrant> Role(string roleCode, DataScope scope, IEnumerable<string> permissions) =>
        permissions.Select(p => new ShippedGrant(roleCode, p, scope));

    private readonly Dictionary<string, PermissionDefinition> _definitions;

    public PermissionCatalogue(IEnumerable<PermissionDefinition> definitions)
    {
        _definitions = definitions.ToDictionary(d => d.Code, StringComparer.Ordinal);
    }

    public IEnumerable<PermissionDefinition> Definitions => _definitions.Values;

    public bool Contains(string code) => _definitions.ContainsKey(code);

    /// <summary>The definition of <paramref name="code"/>. A code outside the catalogue is a programming error, not a denial.</summary>
    public PermissionDefinition Get(string code) =>
        _definitions.TryGetValue(code, out PermissionDefinition? definition)
            ? definition
            : throw new ArgumentException($"'{code}' is not in the permission catalogue.", nameof(code));
}
