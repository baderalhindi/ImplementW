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
            new(NotificationTemplateView, "NOTIFICATIONS", AccessMode.Read),
            new(NotificationTemplateManage, "NOTIFICATIONS", AccessMode.Write),
            new(NotificationDeliveryView, "NOTIFICATIONS", AccessMode.Read),
            new(NotificationDeliveryManage, "NOTIFICATIONS", AccessMode.Write),
            new(LayoutPersonalize, "DASHBOARDS", AccessMode.Write),
            new(ReportCompose, "REPORTS", AccessMode.Write),
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
    /// owners' ASSIGNED grants wait for Appendix A (project-task.md F-2). WF-15's
    /// notification templates and delivery operations go to R01 at ALL, as a delivery-team decision like FG-04's
    /// (notification-runtime.md F-1): they are platform-wide.
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
        new("R02", LayoutPersonalize, DataScope.Own),
        new("R02", ReportCompose, DataScope.Own),
        new("R03", LayoutPersonalize, DataScope.Own),
        new("R03", ReportCompose, DataScope.Own),
        new("R07", LayoutPersonalize, DataScope.Own),
        new("R07", ReportCompose, DataScope.Own),
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
