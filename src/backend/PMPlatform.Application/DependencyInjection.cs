using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Common.Events;
using PMPlatform.Application.Features.Approval;
using PMPlatform.Application.Features.Approval.Contracts;
using PMPlatform.Application.Features.AuditActivity;
using PMPlatform.Application.Features.DocumentManagement;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Administration;
using PMPlatform.Application.Features.IdentityAccess.Authentication;
using PMPlatform.Application.Features.IdentityAccess.Contracts;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.MasterDataConfig;
using PMPlatform.Application.Features.MasterDataConfig.Contracts;
using PMPlatform.Application.Features.MasterDataConfig.Contracts.Resolution;
using PMPlatform.Application.Features.Notifications;
using PMPlatform.Application.Features.Notifications.Contracts;
using PMPlatform.Application.Features.Progress;
using PMPlatform.Application.Features.Progress.Contracts;
using PMPlatform.Application.Features.Project;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.EventHandlers;
using PMPlatform.Application.Features.FinancialKpi;
using PMPlatform.Application.Features.FinancialKpi.Contracts;
using PMPlatform.Application.Features.FinancialKpi.EventHandlers;
using PMPlatform.Application.Features.Milestone;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Application.Features.Milestone.EventHandlers;
using PMPlatform.Application.Features.ManagementConcern;
using PMPlatform.Application.Features.ManagementConcern.Contracts;
using PMPlatform.Application.Features.ManagementConcern.EventHandlers;
using PMPlatform.Application.Features.ProjectTask;
using PMPlatform.Application.Features.ProjectTask.Contracts;
using PMPlatform.Application.Features.Risk;
using PMPlatform.Application.Features.Risk.Contracts;
using PMPlatform.Application.Features.Schedule;
using PMPlatform.Application.Features.Schedule.Contracts;
using PMPlatform.Application.Features.Schedule.EventHandlers;

namespace PMPlatform.Application;

/// <summary>Registers application services. Each module adds its own handlers here as it is built.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        // TASK-033: the formal audit trail every module records into, and the SIEM forwarder. The store and the SIEM
        // client are Infrastructure's; the request context is the API's.
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<ISiemForwarder, SiemForwarder>();

        // IdentityAccess (TASK-028): sign-in and ADM-041. The ports they use are implemented in Infrastructure.
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IIdentityIntegrationService, IdentityIntegrationService>();
        services.AddScoped<IAccessAudit, AccessAudit>();

        // TASK-030: the authorization engine, scoped so it reads the caller's grants once per request.
        services.TryAddSingleton(PermissionCatalogue.Platform);
        services.AddScoped<IAuthorizationEngine, AuthorizationEngine>();

        // TASK-031: FG-03 administration, ADM-002–013. The repositories and the mobile verifier are Infrastructure's.
        services.AddScoped<AdministrationAccess>();
        services.AddScoped<IUserAdministrationService, UserAdministrationService>();
        services.AddScoped<IMobileNumberVerificationService, MobileNumberVerificationService>();
        services.AddScoped<IUserContactDirectory, UserContactDirectory>();
        services.AddScoped<AccessRelationshipService>();
        services.AddScoped<IAccessRelationshipService>(provider => provider.GetRequiredService<AccessRelationshipService>());
        services.AddScoped<IProjectAccessLifecycle>(provider => provider.GetRequiredService<AccessRelationshipService>());
        services.AddScoped<IRoleAdministrationService, RoleAdministrationService>();
        services.AddScoped<IDepartmentAdministrationService, DepartmentAdministrationService>();
        services.AddScoped<IExternalEntityAdministrationService, ExternalEntityAdministrationService>();
        services.AddScoped<IRoleDirectory, RoleDirectory>();
        services.AddScoped<IOrganizationDirectory, OrganizationDirectory>();

        // TASK-034: FG-04 master data and the versioned configuration engine, and the resolution every module uses
        // (E-U2). The repositories are Infrastructure's.
        services.AddScoped<IMasterDataAdministrationService, MasterDataAdministrationService>();
        services.AddScoped<IKpiDefinitionAdministrationService, KpiDefinitionAdministrationService>();
        services.AddScoped<ConfigurationReferenceReader>();
        services.AddScoped<IConfigurationAdministrationService, ConfigurationAdministrationService>();
        services.AddScoped<IConfigurationResolver, ConfigurationResolver>();
        services.AddScoped<IMasterDataResolver, MasterDataResolver>();

        // TASK-035: the WF-11 approval runtime. Source modules start runs through IApprovalRequests and receive outcomes
        // through their IApprovalOutcomeHandler, which the outbox dispatcher reaches through ApprovalOutcomeDispatch.
        services.AddScoped<ApprovalPolicy>();
        services.AddScoped<ApprovalAuthority>();
        services.AddScoped<ApprovalEscalation>();
        services.AddScoped<ApprovalOutcomes>();
        services.AddScoped<IApprovalRunReader, ApprovalRunReader>();
        services.AddScoped<IApprovalRequests, ApprovalRequestService>();
        services.AddScoped<IApprovalWorkflowService, ApprovalWorkflowService>();
        services.AddScoped<IApprovalDelegationService, ApprovalDelegationService>();
        services.AddScoped<IApprovalMaintenance, ApprovalMaintenance>();
        services.AddScoped<IDomainEventConsumer, ApprovalOutcomeDispatch>();

        // TASK-037: WF-12 documents and evidence. The store, the scanner, the upload policy and the repository are
        // Infrastructure's; modules attach documents and pin evidence through IDocumentLinks.
        services.AddScoped<DocumentAccess>();
        services.AddScoped<DocumentReferences>();
        services.AddScoped<DocumentUploads>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IDocumentLinks, DocumentLinkService>();
        services.AddScoped<IDocumentScanning, DocumentScanning>();

        // TASK-039: the WF-15 runtime. Intents arrive through the outbox dispatcher after their source committed; the worker
        // routes and sends them. The channels, the repository and the recipient directory are Infrastructure's; a source
        // module that schedules reminders registers its INotificationConditionSource.
        services.AddScoped<INotificationIntentConsumer, NotificationIntentIntake>();
        services.AddScoped<NotificationRouting>();
        services.AddScoped<NotificationSending>();
        services.AddScoped<INotificationProcessing, NotificationProcessing>();
        services.AddScoped<INotificationInbox, NotificationInbox>();
        services.AddScoped<INotificationTemplateAdministration, NotificationTemplateAdministration>();
        services.AddScoped<INotificationOperations, NotificationOperations>();

        // TASK-041: WF-01 registration and activation. The repository is Infrastructure's; review outcomes arrive through
        // the Project approval outcome handler (WF-11 edge 28).
        services.AddScoped<ProjectAccess>();
        services.AddScoped<ProjectReferences>();
        services.AddScoped<ProjectManagerEligibility>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IApprovalOutcomeHandler, ProjectApprovalOutcomeHandler>();
        services.AddScoped<IProjectFactsReader, ProjectFactsReader>();

        // TASK-044: WF-02 progress reporting and Overall Project Health (ICD-03). The repository is Infrastructure's. The
        // inputs other modules own have no source until WF-03, WF-04 and WF-14 and their ADR-003 edges exist
        // (progress-update.md F-1), so NoProgressInputs stands in; a host may register its own first.
        services.AddScoped<ProgressAccess>();
        services.AddScoped<ProgressPolicy>();
        services.TryAddScoped<IProgressInputs, NoProgressInputs>();
        services.AddScoped<IProgressService, ProgressService>();
        services.AddScoped<ProgressOpeningPosition>();
        services.AddScoped<IProjectHealthReader, ProjectHealthReader>();
        services.AddScoped<IReportingCycleReader, ReportingCycleReader>();

        // TASK-046: WF-03 schedule and baselines. The repository is Infrastructure's; baseline outcomes arrive through the
        // Schedule approval outcome handler (WF-11 edges 21, 28), and Project's activation command consults the baseline
        // precondition (ADR-009). No change authorisation can be read until TASK-060 (schedule-baseline.md F-1), so
        // NoRebaselineAuthorization stands in; a host may register its own first.
        services.AddScoped<ScheduleAccess>();
        services.AddScoped<SchedulePolicy>();
        services.AddScoped<ScheduleHealthProjection>();
        services.AddScoped<BaselineActivation>();
        services.TryAddScoped<IRebaselineAuthorization, NoRebaselineAuthorization>();
        services.AddScoped<IScheduleService, ScheduleService>();
        services.AddScoped<IBaselineService, BaselineService>();
        services.AddScoped<IApprovalOutcomeHandler, BaselineApprovalOutcomeHandler>();
        services.AddScoped<IProjectActivationPrecondition, BaselineActivationPrecondition>();
        services.AddScoped<ScheduleDeclaredBaseline>();
        services.AddScoped<IScheduleHealthReader, ScheduleHealthReader>();
        services.AddScoped<IScheduleActivityReader, ScheduleActivityReader>();

        // TASK-050: WF-03's side of the shared milestone (ICD-04) — the milestones of the schedule, and the contract WF-05 reads
        // and records an acceptance through (edge 10).
        services.AddScoped<IProjectMilestoneService, ProjectMilestoneService>();
        services.AddScoped<IProjectMilestoneReader, ProjectMilestoneReader>();
        services.AddScoped<IMilestoneAchievementRecorder, MilestoneAchievementRecorder>();

        // TASK-048: WF-04 tasks, their dependencies and the Activity Execution Progress. The repository is Infrastructure's;
        // schedule activities are read through Schedule's reader (edge 9) and the project's facts through Project's (edge 36).
        services.AddScoped<ProjectTaskAccess>();
        services.AddScoped<ProjectTaskReferences>();
        services.AddScoped<ActivityProgressProjection>();
        services.AddScoped<ProjectTaskGate>();
        services.AddScoped<IProjectTaskService, ProjectTaskService>();
        services.AddScoped<ITaskExecutionService, TaskExecutionService>();
        services.AddScoped<ITaskDependencyService, TaskDependencyService>();

        // TASK-050: WF-05 achievement claims and their revisions. The repository is Infrastructure's; the milestone is WF-03's,
        // read through Schedule's reader (edge 10); acceptance comes from WF-11 through the outcome handler (edges 25, 28).
        services.AddScoped<MilestoneAccess>();
        services.AddScoped<MilestoneEvidencePolicy>();
        services.AddScoped<IMilestoneAchievementService, MilestoneAchievementService>();
        services.AddScoped<IApprovalOutcomeHandler, MilestoneAchievementOutcomeHandler>();

        // TASK-052: WF-14's two subdomains. Financial Progress — source modes, Approved Budget versions (WF-11, edge 26; documents,
        // edge 38), periodic updates aligned to WF-02's periods (edge 13) and their published snapshots; KPI Performance — assignments,
        // target versions (WF-11, edge 26) and pinned measurements. The repository is Infrastructure's.
        services.AddScoped<FinancialKpiAccess>();
        services.AddScoped<FinancialPolicy>();
        services.AddScoped<KpiDefinitions>();
        services.AddScoped<KpiScope>();
        services.AddScoped<IFinancialSourceModeService, FinancialSourceModeService>();
        services.AddScoped<IFinancialCommitmentService, FinancialCommitmentService>();
        services.AddScoped<IFinancialProgressService, FinancialProgressService>();
        services.AddScoped<IKpiAssignmentService, KpiAssignmentService>();
        services.AddScoped<IKpiTargetVersionService, KpiTargetVersionService>();
        services.AddScoped<IKpiMeasurementService, KpiMeasurementService>();
        services.AddScoped<IApprovalOutcomeHandler, FinancialKpiApprovalOutcomeHandler>();

        // TASK-055: WF-06's risk register — risks, assessment versions pinned to the RISK_MATRIX version in force, treatment actions,
        // time-bound acceptances and their expiry, and the reminder condition source. Materialisation into an issue goes through
        // WF-07's IRiskIssueMaterialisation (edge 15). The repository is Infrastructure's.
        services.AddScoped<RiskAccess>();
        services.AddScoped<RiskReferences>();
        services.AddScoped<RiskViews>();
        services.AddScoped<RiskGate>();
        services.AddScoped<IRiskService, RiskService>();
        services.AddScoped<IRiskLifecycleService, RiskLifecycleService>();
        services.AddScoped<IRiskTreatmentService, RiskTreatmentService>();
        services.AddScoped<IRiskMaintenance, RiskMaintenance>();
        services.AddScoped<INotificationConditionSource, RiskConditionSource>();

        // TASK-057: WF-07's issues and challenges — severity computed from impacts on the shared scale and pinned, escalations published
        // to WF-15 once each, resolutions validated through WF-11 (edges 24, 28), and the issue side of edge 15. The repository is
        // Infrastructure's.
        services.AddScoped<ConcernAccess>();
        services.AddScoped<ConcernReferences>();
        services.AddScoped<ConcernSeverity>();
        services.AddScoped<ConcernViews>();
        services.AddScoped<ConcernGate>();
        services.AddScoped<ConcernIntake>();
        services.AddScoped<IManagementConcernService, ManagementConcernService>();
        services.AddScoped<IConcernLifecycleService, ConcernLifecycleService>();
        services.AddScoped<IConcernEscalationService, ConcernEscalationService>();
        services.AddScoped<IApprovalOutcomeHandler, ConcernValidationOutcomeHandler>();
        services.AddScoped<IRiskIssueMaterialisation, RiskIssueRegister>();

        return services;
    }
}
