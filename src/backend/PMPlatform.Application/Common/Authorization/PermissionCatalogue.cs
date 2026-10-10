using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Common.Authorization;

/// <summary>
/// The protected permission catalogue (ERD F-081) and the grants of the R01–R08 shipped-default profiles. db/seed
/// writes both to <c>identity_access</c>, and a seed test fails if the two disagree.
/// </summary>
/// <remarks>
/// Blueprint Appendix A, the full matrix, is not in the repository (controlled-source-baseline.md §5.1, seed record
/// F-1). Only rows a controlled source fixes for all eight roles are here; the rest are added from Appendix A.
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
        ]);

    /// <summary>
    /// ADM-002–013 are R01's alone: TASK-032's acceptance criterion has every FG-03 administration screen "reachable only
    /// for R01 per RBAC". ALL, because an administrator administers every user and structure. FG-04 (ADM-020–029 and
    /// configuration) goes to R01 at ALL too, as a delivery-team decision no controlled source states yet (TASK-034 F-1).
    /// Master data and configuration are platform-wide, with no record to scope. ADR-019 grants Personalize Layout and Compose Report to R02, R03 and R07 and withholds them from R04, R05, R06 and
    /// R08; each acts on the holder's own layout or report definition, hence OWN (the ADR names no scope). The report's
    /// data is authorised independently for every viewer (CTL-15). ADR-013's participation amendment to TASK-037 lets an
    /// entity Project Manager upload, view and see the version history of documents on their own project within
    /// classification rules: R04 at ENTITY. The scope reaches only a holder with an entity, so an internal R04 gains
    /// nothing from it, and an external holder's per-project assignment keeps it to that project (ADR-013). The other
    /// document grants wait for Appendix A (document-management.md F-1). ADR-013's amendment to TASK-041 lets external entity
    /// users create a project draft for their own entity, and lets entities see their own projects: R08 (External Entity
    /// User) and R04 (the entity Project Manager) view and register at ENTITY, which reaches only a holder with an entity.
    /// AHDA's review and activation gates are internal-only and wait for Appendix A (project-registration.md F-1). ADR-013's
    /// amendment to TASK-044 lets an assigned entity Project Manager submit progress on their own project, and lets entities
    /// see progress and health on their own projects: R04 views and submits at OWN — a project's owner is its Project Manager,
    /// so OWN reaches exactly the projects the holder manages, internal or external, R04 being employer-neutral — and R08
    /// views at ENTITY. Progress review is AHDA's gate and waits for Appendix A (progress-update.md F-2). WF-03's functional
    /// specification §3 makes the Project Manager (R04) the schedule's owner — building it, keeping its forecast, submitting its
    /// baselines — so R04 views and edits schedules at OWN, the projects the holder manages, internal or entity alike (ADR-013);
    /// the other roles' schedule grants wait for Appendix A (schedule-baseline.md F-2). WF-04's four task permissions go to R04
    /// at OWN: the Project Manager plans the project's tasks, may edit a task's actual percentage (ADR-009) and holds the
    /// controlled reopen, on the projects the holder manages, internal or entity (ADR-013's amendment to TASK-048); the task
    /// owners' ASSIGNED grants wait for Appendix A (project-task.md F-2). WF-05's view and submit go to R04 at OWN: ADR-013's
    /// amendment to TASK-050 lets an entity Project Manager submit achievement claims on their own project, and OWN reaches the
    /// projects the holder manages, internal or entity; acceptance is WF-11's and the other roles' milestone grants wait for
    /// Appendix A (milestone-achievement.md F-2). ADR-013's amendment to TASK-055 lets entity Project Managers register and update risks on
    /// their own project: R04 views and manages risks at OWN, the projects the holder manages, internal or entity. Rating and
    /// acceptance authority is unchanged — RISK_ASSESS and RISK_ACCEPT are refused to an external user whatever they hold — and
    /// those grants, with the reopen, wait for Appendix A (risk-management.md F-2). ADR-013's amendment to TASK-057 lets entities raise a
    /// blocker or an issue on their own project and see its status — intake and visibility, not escalation: R08 views and raises concerns
    /// at ENTITY, and R04 at OWN, the projects the holder manages, internal or entity. WF-07's specification §3 makes the Project Manager
    /// the primary manager of a project's issues and challenges, who assesses, assigns, plans resolution and escalates: R04 manages and
    /// escalates at OWN, and the application refuses both to an external user whatever they hold. §3 gives the Department Manager
    /// "escalation handling": R03 views concerns and resolves escalations at DEPT, through the role an escalation is addressed to. The
    /// other roles' concern grants wait for Appendix A (management-concern.md F-2). ADR-013's amendment to TASK-060 lets entity Project
    /// Managers raise a change request, and keeps materiality, approval and implementation AHDA's: R04 views and raises change requests
    /// at OWN, the projects the holder manages, internal or entity. WF-08's specification §3 makes the Project Manager the coordinator of
    /// a change's implementation and the Department Manager its reviewer: R04 implements at OWN — the application refuses an external
    /// holder — and R03 views and starts reviews at DEPT. Approval is WF-11's APPROVAL_DECIDE; the other roles' grants wait for Appendix A
    /// (change-request.md F-2). WF-09's specification §3 makes the Project Manager the one who prepares and submits a suspension or
    /// resumption request and the Department Manager its reviewer; ADR-013 has the entity fill in the information and AHDA approve: R04
    /// views and raises requests at OWN, the projects the holder manages, internal or entity, and R03 views and starts reviews at DEPT.
    /// Approval is WF-11's APPROVAL_DECIDE. Activation is WF-09's own service by default (§14), so SUSPENSION_ACTIVATE ships to no role;
    /// it and the other roles' grants wait for Appendix A (suspension.md F-2). WF-10's specification §11 and §13 make the Project Manager
    /// the one who prepares completion and closure, and the Department Manager the reviewer of the immutable submission and its readiness
    /// exceptions (US-CLO-DM-002 to -004); ADR-013 has the entity fill in the information and AHDA approve: R04 views and raises cases at
    /// OWN, internal or entity, and R03 views, starts reviews and waives criteria at DEPT. Approval is WF-11's APPROVAL_DECIDE.
    /// Activation is WF-10's own service by default (§13 Completion.Activate: "System/service"), so CLOSEOUT_ACTIVATE ships to no role; it
    /// and the other roles' grants wait for Appendix A (closure.md F-2). WF-13's specification §13 (TASK-066) makes the
    /// Project Manager the one who requests information from an entity, reviews the answer when assigned and, as the source owner, applies it
    /// (US-EXT-PM-001, -012 to -017): R04 views, manages, reviews and applies external update requests at OWN. The Department Manager requests
    /// and reviews when assigned (US-EXT-DM-003, -006, -009): R03 views, manages and reviews at DEPT. The external entity user sees its own
    /// entity's requests at ENTITY — a per-project assignment narrows it to that project — and answers only those it is named on, at ASSIGNED
    /// (EXT-CC-03, EXT-CC-04). Requesting, reviewing and applying are refused to an external user whatever they hold: AHDA issues update
    /// requests and reviews them (TASK-066 gate decision; ADR-013). The same gate decision limits an external contributor's direct source
    /// action to assigned tasks (WF-13 Path A): R08 views and updates WF-04 tasks at ASSIGNED, which reaches the tasks the holder owns and
    /// nothing else. The rest, R05's liaison grants among them, wait for Appendix A (external-participation.md F-2). ADR-013's amendment to
    /// TASK-052 lets an entity see budget, expenditure and KPI
    /// status for its own project, masked by audience per ADR-010: R08 and R04 view financials and KPIs at ENTITY, which reaches
    /// only a holder with an entity; entering, reviewing and source configuration wait for Appendix A (financial-kpi.md F-2). WF-15's
    /// notification templates and delivery operations go to R01 at ALL, as a delivery-team decision like FG-04's
    /// (notification-runtime.md F-1): they are platform-wide. ADR-013's amendment to TASK-071 lets an entity run and export a
    /// defined report set on its own projects, financial fields masked: R08 and R04 export reports at ENTITY, which reaches only
    /// a holder with an entity. FG-02 §18 makes export Conditional for every internal role: those grants wait for Appendix A
    /// (reports.md F-3).
    /// </summary>
    public static IReadOnlyList<ShippedGrant> ShippedDefaultGrants { get; } =
    [
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
        new("R04", DocumentView, DataScope.Entity),
        new("R04", DocumentUpload, DataScope.Entity),
        new("R04", ProjectView, DataScope.Entity),
        new("R04", ProjectRegister, DataScope.Entity),
        new("R08", ProjectView, DataScope.Entity),
        new("R08", ProjectRegister, DataScope.Entity),
        new("R04", ProgressView, DataScope.Own),
        new("R04", ProgressSubmit, DataScope.Own),
        new("R08", ProgressView, DataScope.Entity),
        new("R04", ScheduleView, DataScope.Own),
        new("R04", ScheduleEdit, DataScope.Own),
        new("R04", TaskView, DataScope.Own),
        new("R04", TaskUpdate, DataScope.Own),
        new("R04", TaskManage, DataScope.Own),
        new("R04", TaskReopen, DataScope.Own),
        new("R04", MilestoneView, DataScope.Own),
        new("R04", MilestoneSubmit, DataScope.Own),
        new("R04", RiskView, DataScope.Own),
        new("R04", RiskManage, DataScope.Own),
        new("R04", ConcernView, DataScope.Own),
        new("R04", ConcernRaise, DataScope.Own),
        new("R04", ConcernManage, DataScope.Own),
        new("R04", ConcernEscalate, DataScope.Own),
        new("R08", ConcernView, DataScope.Entity),
        new("R08", ConcernRaise, DataScope.Entity),
        new("R03", ConcernView, DataScope.Dept),
        new("R03", ConcernEscalationResolve, DataScope.Dept),
        new("R04", ChangeRequestView, DataScope.Own),
        new("R04", ChangeRequestRaise, DataScope.Own),
        new("R04", ChangeRequestImplement, DataScope.Own),
        new("R03", ChangeRequestView, DataScope.Dept),
        new("R03", ChangeRequestReview, DataScope.Dept),
        new("R04", SuspensionView, DataScope.Own),
        new("R04", SuspensionRaise, DataScope.Own),
        new("R03", SuspensionView, DataScope.Dept),
        new("R03", SuspensionReview, DataScope.Dept),
        new("R04", CloseoutView, DataScope.Own),
        new("R04", CloseoutRaise, DataScope.Own),
        new("R03", CloseoutView, DataScope.Dept),
        new("R03", CloseoutReview, DataScope.Dept),
        new("R03", CloseoutWaive, DataScope.Dept),
        new("R08", TaskView, DataScope.Assigned),
        new("R08", TaskUpdate, DataScope.Assigned),
        new("R04", ExternalRequestView, DataScope.Own),
        new("R04", ExternalRequestManage, DataScope.Own),
        new("R04", ExternalContributionReview, DataScope.Own),
        new("R04", ExternalContributionApply, DataScope.Own),
        new("R03", ExternalRequestView, DataScope.Dept),
        new("R03", ExternalRequestManage, DataScope.Dept),
        new("R03", ExternalContributionReview, DataScope.Dept),
        new("R08", ExternalRequestView, DataScope.Entity),
        new("R08", ExternalContributionRespond, DataScope.Assigned),
        new("R04", FinancialView, DataScope.Entity),
        new("R08", FinancialView, DataScope.Entity),
        new("R04", KpiView, DataScope.Entity),
        new("R08", KpiView, DataScope.Entity),
        new("R02", LayoutPersonalize, DataScope.Own),
        new("R02", ReportCompose, DataScope.Own),
        new("R03", LayoutPersonalize, DataScope.Own),
        new("R03", ReportCompose, DataScope.Own),
        new("R07", LayoutPersonalize, DataScope.Own),
        new("R07", ReportCompose, DataScope.Own),
        new("R04", ReportExport, DataScope.Entity),
        new("R08", ReportExport, DataScope.Entity),
    ];

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
