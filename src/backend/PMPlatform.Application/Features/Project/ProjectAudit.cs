using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Project.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Project;
using ProjectEntity = PMPlatform.Domain.Project.Project;

namespace PMPlatform.Application.Features.Project;

/// <summary>The audit events of WF-01 (TASK-033 <c>IAuditTrail</c>). Titles and descriptions are free text and are not copied.</summary>
internal static class ProjectAudit
{
    public static AuditEntry Created(Guid actorId, ProjectEntity project) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.ProjectCreated, actorId, project,
        [
            AuditAttribute.Of(ProjectAuditAttributes.Status, project.LifecycleState),
            .. Changes(null, project),
        ]);

    /// <summary>The fields that changed between <paramref name="before"/> and the project as it is now.</summary>
    public static AuditEntry Changed(Guid actorId, ProjectDraft before, ProjectEntity project) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.ProjectChanged, actorId, project, Changes(before, project));

    public static AuditEntry Deleted(Guid actorId, ProjectEntity project) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.ProjectDeleted, actorId, project, [AuditAttribute.Of(ProjectAuditAttributes.Status, project.LifecycleState)]);

    public static AuditEntry Submitted(Guid actorId, ProjectEntity project, ProjectLifecycleState from, int fromRevisionNo, Guid? fromManagerId) =>
        Transition(ProjectAuditEvents.ProjectSubmitted, actorId, project, from, fromRevisionNo,
            [AuditAttribute.Change(ProjectAuditAttributes.ProjectManagerUserId, fromManagerId, project.ProjectManagerUserId)]);

    public static AuditEntry Withdrawn(Guid actorId, ProjectEntity project, Guid? fromManagerId) =>
        Transition(ProjectAuditEvents.SubmissionWithdrawn, actorId, project, ProjectLifecycleState.Submitted, project.RevisionNo,
            [AuditAttribute.Change(ProjectAuditAttributes.ProjectManagerUserId, fromManagerId, project.ProjectManagerUserId)]);

    public static AuditEntry ReviewStarted(Guid actorId, ProjectEntity project, Guid approvalInstanceId) =>
        Transition(ProjectAuditEvents.ReviewStarted, actorId, project, ProjectLifecycleState.Submitted, project.RevisionNo,
            [AuditAttribute.Of(ProjectAuditAttributes.ApprovalInstanceId, approvalInstanceId)]);

    public static AuditEntry Returned(ProjectEntity project, ApprovalOutcomeRecorded outcome) =>
        Transition(ProjectAuditEvents.RegistrationReturned, outcome.Data.DecidedByUserId, project, ProjectLifecycleState.UnderReview, project.RevisionNo,
            OutcomeAttributes(outcome));

    public static AuditEntry Approved(ProjectEntity project, ApprovalOutcomeRecorded outcome) =>
        Transition(ProjectAuditEvents.RegistrationApproved, outcome.Data.DecidedByUserId, project, ProjectLifecycleState.UnderReview, project.RevisionNo,
            [.. OutcomeAttributes(outcome), AuditAttribute.Change(ProjectAuditAttributes.FormalProjectId, null, project.FormalProjectId)]);

    public static AuditEntry Activated(Guid actorId, ProjectEntity project) =>
        Transition(ProjectAuditEvents.ProjectActivated, actorId, project, ProjectLifecycleState.ApprovedPlanned, project.RevisionNo,
            [AuditAttribute.Of(ProjectAuditAttributes.ActivatedAt, project.ActivatedAt)]);

    /// <summary>EV-5: an outcome for a revision the project has moved past, or for a review it is no longer under, is not applied.</summary>
    public static AuditEntry OutcomeIgnored(ProjectEntity project, ApprovalOutcomeRecorded outcome) =>
        new(AuditEventClass.LifecycleTransition, ProjectAuditEvents.ApprovalOutcomeIgnored, AuditOutcome.Failed)
        {
            ActorUserId = outcome.Data.DecidedByUserId,
            Subject = SubjectOf(project),
            ScopeProjectId = ScopeOf(project),
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                .. OutcomeAttributes(outcome),
                AuditAttribute.Of(ProjectAuditAttributes.SubjectRevisionNo, outcome.Subject.RevisionNo),
                AuditAttribute.Of(ProjectAuditAttributes.Status, project.LifecycleState),
                AuditAttribute.Of(ProjectAuditAttributes.RevisionNo, project.RevisionNo),
            ],
        };

    /// <summary>ADR-013: an external user asked for one of AHDA's lifecycle gates.</summary>
    public static AuditEntry GateRefused(Guid actorId, ProjectEntity project, string permissionCode) =>
        new(AuditEventClass.AuthorizationDenial, ProjectAuditEvents.LifecycleGateRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = SubjectOf(project),
            ScopeProjectId = ScopeOf(project),
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(ProjectAuditAttributes.Gate, permissionCode),
                AuditAttribute.Of(ProjectAuditAttributes.Status, project.LifecycleState),
                AuditAttribute.Of(ProjectAuditAttributes.Reason, "EXTERNAL_USER"),
            ],
        };

    /// <summary>The state change, the revision (as a change when it moved), and what else the transition records.</summary>
    private static AuditEntry Transition(
        string eventType, Guid actorId, ProjectEntity project, ProjectLifecycleState from, int fromRevisionNo, IEnumerable<AuditAttribute?> attributes) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project,
        [
            AuditAttribute.Change(ProjectAuditAttributes.Status, from, project.LifecycleState)!,
            AuditAttribute.Change(ProjectAuditAttributes.RevisionNo, fromRevisionNo, project.RevisionNo) ?? AuditAttribute.Of(ProjectAuditAttributes.RevisionNo, project.RevisionNo),
            .. attributes.OfType<AuditAttribute>(),
        ]);

    private static AuditEntry Entry(AuditEventClass eventClass, string eventType, Guid actorId, ProjectEntity project, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = SubjectOf(project),
            ScopeProjectId = ScopeOf(project),
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    /// <summary>
    /// A project is scoped on its audit events once it has entered review (record F-5). Before that it is a deletable draft
    /// (HARD_DRAFT), and <c>audit_event.scope_project_id</c>'s foreign key would keep it from ever being deleted; those events
    /// are found by their subject. From review on, its approval history references it and it is never deleted anyway.
    /// </summary>
    private static Guid? ScopeOf(ProjectEntity project) =>
        project.LifecycleState is ProjectLifecycleState.Draft or ProjectLifecycleState.Submitted ? null : project.Id;

    private static AuditSubject SubjectOf(ProjectEntity project) => new(ProjectApprovalRouting.SubjectModule, ProjectApprovalRouting.SubjectType, project.Id);

    private static AuditAttribute[] OutcomeAttributes(ApprovalOutcomeRecorded outcome) =>
    [
        AuditAttribute.Of(ProjectAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
        AuditAttribute.Of(ProjectAuditAttributes.Decision, outcome.Data.Decision),
    ];

    /// <summary>Each registration field that differs between <paramref name="before"/> (null: nothing before) and the project.</summary>
    private static IEnumerable<AuditAttribute> Changes(ProjectDraft? before, ProjectEntity after) =>
        new[]
        {
            AuditAttribute.WithheldChange(ProjectAuditAttributes.TitleText, before?.Title.Text, after.Title.Text),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.DescriptionText, before?.Description?.Text, after.Description?.Text),
            AuditAttribute.Change(ProjectAuditAttributes.ClassificationItemId, before?.ClassificationItemId, after.ClassificationItemId),
            AuditAttribute.Change(ProjectAuditAttributes.DepartmentId, before?.DepartmentId, after.DepartmentId),
            AuditAttribute.Change(ProjectAuditAttributes.ExternalEntityId, before?.ExternalEntityId, after.ExternalEntityId),
            AuditAttribute.Change(ProjectAuditAttributes.ParticipationMode, before?.ParticipationMode, after.ParticipationMode),
            AuditAttribute.Change(ProjectAuditAttributes.GovernanceProfileItemId, before?.GovernanceProfileItemId, after.GovernanceProfileItemId),
            AuditAttribute.Change(ProjectAuditAttributes.RegistrationBudgetSar, before?.RegistrationBudgetSar?.Amount, after.RegistrationBudgetSar?.Amount),
            AuditAttribute.Change(ProjectAuditAttributes.PlannedStartDate, before?.PlannedStartDate, after.PlannedStartDate),
            AuditAttribute.Change(ProjectAuditAttributes.PlannedEndDate, before?.PlannedEndDate, after.PlannedEndDate),
            AuditAttribute.Change(ProjectAuditAttributes.RegionItemId, before?.RegionItemId, after.RegionItemId),
            AuditAttribute.Change(ProjectAuditAttributes.CityItemId, before?.CityItemId, after.CityItemId),
            AuditAttribute.Change(ProjectAuditAttributes.Latitude, before?.Latitude, after.Latitude),
            AuditAttribute.Change(ProjectAuditAttributes.Longitude, before?.Longitude, after.Longitude),
        }.OfType<AuditAttribute>();
}
