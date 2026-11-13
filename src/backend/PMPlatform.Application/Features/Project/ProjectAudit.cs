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

    /// <summary>
    /// ADR-014's intake: the project enters ACTIVE with its Formal Project ID, its manager and the intake it was taken in by, and every
    /// registration field as a creation does. The declared scope is free text and is not copied.
    /// </summary>
    public static AuditEntry TakenIn(Guid actorId, ProjectEntity project, ProjectIntake intake) =>
        Entry(AuditEventClass.LifecycleTransition, ProjectAuditEvents.ProjectTakenIn, actorId, project,
        [
            AuditAttribute.Change(ProjectAuditAttributes.Status, null, project.LifecycleState)!,
            AuditAttribute.Of(ProjectAuditAttributes.FormalProjectId, project.FormalProjectId),
            AuditAttribute.Of(ProjectAuditAttributes.ProjectManagerUserId, project.ProjectManagerUserId),
            AuditAttribute.Of(ProjectAuditAttributes.LegacyIntakeDate, project.LegacyIntakeDate),
            AuditAttribute.Of(ProjectAuditAttributes.ProjectIntakeId, intake.Id),
            AuditAttribute.Of(ProjectAuditAttributes.DeclaredBudgetSar, intake.DeclaredBudgetSar.Amount),
            AuditAttribute.Of(ProjectAuditAttributes.DeclaredEndDate, intake.DeclaredEndDate),
            AuditAttribute.Of(ProjectAuditAttributes.OpeningPercentComplete, intake.OpeningPercentComplete),
            AuditAttribute.Of(ProjectAuditAttributes.OpeningSpendToDateSar, intake.OpeningSpendToDateSar.Amount),
            .. Changes(null, project),
        ]);

    /// <summary>
    /// WF-09's ACTIVE → SUSPENDED or SUSPENDED → ACTIVE (edge 7), by whoever effected the request — a person, or WF-09's service
    /// principal — naming the request effected.
    /// </summary>
    public static AuditEntry SuspensionTransition(ProjectSuspensionCommand command, ProjectEntity project, ProjectLifecycleState from)
    {
        AuditEntry entry = Transition(
            project.LifecycleState == ProjectLifecycleState.Suspended ? ProjectAuditEvents.ProjectSuspended : ProjectAuditEvents.ProjectResumed,
            command.ActorId, project, from, project.RevisionNo, [AuditAttribute.Of(ProjectAuditAttributes.SuspensionRequestId, command.SuspensionRequestId)]);
        return entry with { ActorType = command.ActorType };
    }

    /// <summary>
    /// WF-10's ACTIVE → COMPLETED, COMPLETED → CLOSED or SUSPENDED → CLOSED (edge 8), by whoever effected the case — a person, or WF-10's
    /// service principal — naming the completion or closure case effected.
    /// </summary>
    public static AuditEntry CloseoutTransition(ProjectCloseoutCommand command, ProjectEntity project, ProjectLifecycleState from)
    {
        bool completed = project.LifecycleState == ProjectLifecycleState.Completed;
        AuditEntry entry = Transition(
            completed ? ProjectAuditEvents.ProjectCompleted : ProjectAuditEvents.ProjectClosed,
            command.ActorId, project, from, project.RevisionNo,
            [
                AuditAttribute.Of(completed ? ProjectAuditAttributes.CompletionCaseId : ProjectAuditAttributes.ClosureCaseId, command.CaseId),
                completed ? null : AuditAttribute.Of(ProjectAuditAttributes.ClosedAt, project.ClosedAt),
            ]);
        return entry with { ActorType = command.ActorType };
    }

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
            AuditAttribute.Change(ProjectAuditAttributes.StrategicPortfolioItemId, before?.StrategicPortfolioItemId, after.StrategicPortfolioItemId),
            AuditAttribute.Change(ProjectAuditAttributes.StrategicInitiativeItemId, before?.StrategicInitiativeItemId, after.StrategicInitiativeItemId),
            AuditAttribute.Change(ProjectAuditAttributes.PriorityItemId, before?.PriorityItemId, after.PriorityItemId),
            AuditAttribute.Change(ProjectAuditAttributes.FundingSource, before?.FundingSource, after.FundingSource),
            AuditAttribute.Change(ProjectAuditAttributes.LegacyReference, before?.LegacyReference, after.LegacyReference),
            AuditAttribute.Change(ProjectAuditAttributes.FollowUpOwnerUserId, before?.FollowUpOwnerUserId, after.FollowUpOwnerUserId),
            AuditAttribute.Change(ProjectAuditAttributes.ProposedProjectManagerUserId, before?.ProposedProjectManagerUserId, after.ProposedProjectManagerUserId),
        }.OfType<AuditAttribute>();

    /// <summary>R3-3: the follow-up fields that changed. The obstacle and next action are free text and are not copied.</summary>
    public static AuditEntry FollowUpChanged(Guid actorId, ProjectFollowUpDetail before, ProjectEntity project) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.FollowUpChanged, actorId, project, FollowUpChanges(before, project));

    public static AuditEntry FollowUpNoteAdded(Guid actorId, ProjectEntity project, ProjectFollowUpNote note) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.FollowUpNoteAdded, actorId, project, [AuditAttribute.Of(ProjectAuditAttributes.FollowUpNoteId, note.Id)]);

    /// <summary>Review round 7, item 4: each registration field that changed, old and new, and the reason (withheld text).</summary>
    public static AuditEntry RegistrationEdited(Guid actorId, ProjectDraft before, ProjectEntity project, NarrativeText reason) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.RegistrationEdited, actorId, project,
        [
            .. Changes(before, project),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.ReasonText, null, reason.Text)!,
        ]);

    /// <summary>Review round 7, item 2: the manager before and after, the reason (withheld text), and the open records that changed hands, by kind.</summary>
    public static AuditEntry ManagerChanged(Guid actorId, ProjectEntity project, Guid? fromManagerId, NarrativeText reason, IReadOnlyDictionary<string, int> moved) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.ManagerChanged, actorId, project, new[]
        {
            AuditAttribute.Change(ProjectAuditAttributes.ProjectManagerUserId, fromManagerId, project.ProjectManagerUserId),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.ReasonText, null, reason.Text),
        }
        .Concat(moved.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => AuditAttribute.Of($"moved_{m.Key}", m.Value)))
        .OfType<AuditAttribute>());

    /// <summary>Review round 7, item 1: each stage whose plan changed, the plan's revision, and the authorisation and reason of a change after approval.</summary>
    public static AuditEntry StagePlanChanged(
        Guid actorId, ProjectEntity project, int fromRevisionNo, IReadOnlyDictionary<string, string?> before, IReadOnlyDictionary<string, string?> after,
        Guid? changeAuthorizationId, NarrativeText? reason) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.StagePlanChanged, actorId, project, new[]
        {
            AuditAttribute.Change(ProjectAuditAttributes.StagePlanRevisionNo, fromRevisionNo, project.StagePlanRevisionNo),
            changeAuthorizationId is null ? null : AuditAttribute.Of(ProjectAuditAttributes.ChangeAuthorizationId, changeAuthorizationId),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.ReasonText, null, reason?.Text),
        }
        .Concat(after.Keys.Union(before.Keys).Order(StringComparer.Ordinal)
            .Select(code => AuditAttribute.Change(ProjectAuditAttributes.StagePlanOf(code), before.GetValueOrDefault(code), after.GetValueOrDefault(code))))
        .OfType<AuditAttribute>());

    /// <summary>R3-4: what an approved import batch changed on an existing project, its classification and its follow-up.</summary>
    public static AuditEntry UpdatedByImport(Guid actorId, ProjectDraft before, ProjectFollowUpDetail followUpBefore, ProjectEntity project, Guid batchId) =>
        Entry(AuditEventClass.DataChange, ProjectAuditEvents.ProjectUpdatedByImport, actorId, project,
        [
            AuditAttribute.Of(ProjectAuditAttributes.ProjectImportBatchId, batchId),
            .. Changes(before, project),
            .. FollowUpChanges(followUpBefore, project),
        ]);

    private static IEnumerable<AuditAttribute> FollowUpChanges(ProjectFollowUpDetail before, ProjectEntity after) =>
        new[]
        {
            AuditAttribute.WithheldChange(ProjectAuditAttributes.CurrentObstacleText, before.CurrentObstacle?.Text, after.CurrentObstacle?.Text),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.NextActionText, before.NextAction?.Text, after.NextAction?.Text),
            AuditAttribute.WithheldChange(ProjectAuditAttributes.NextActionOwner, before.NextActionOwner, after.NextActionOwner),
            AuditAttribute.Change(ProjectAuditAttributes.NextActionDueDate, before.NextActionDueDate, after.NextActionDueDate),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredPlannedPercent, before.DeclaredPlannedPercent, after.DeclaredPlannedPercent),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredActualPercent, before.DeclaredActualPercent, after.DeclaredActualPercent),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredPlannedSpendPercent, before.DeclaredPlannedSpendPercent, after.DeclaredPlannedSpendPercent),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredActualSpendPercent, before.DeclaredActualSpendPercent, after.DeclaredActualSpendPercent),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredAsOf, before.DeclaredAsOf, after.DeclaredAsOf),
            AuditAttribute.Change(ProjectAuditAttributes.DeclaredSource, before.DeclaredSource, after.DeclaredSource),
            AuditAttribute.Change(ProjectAuditAttributes.EntityUpdateCompliant, before.EntityUpdateCompliant, after.EntityUpdateCompliant),
        }.OfType<AuditAttribute>();
}
