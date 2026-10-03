using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.Milestone.Contracts;
using PMPlatform.Application.Features.Milestone.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Milestone;

namespace PMPlatform.Application.Features.Milestone;

/// <summary>The audit events of WF-05 (TASK-033 <c>IAuditTrail</c>). Narratives and reasons are free text and are not copied.</summary>
internal static class MilestoneAudit
{
    /// <summary>Why an approved revision was returned instead of accepted: its milestone was cancelled in the schedule meanwhile.</summary>
    public const string MilestoneCancelled = "MILESTONE_CANCELLED";

    public static AuditEntry Started(Guid actorId, ProjectFacts project, MilestoneAchievement achievement, Guid? correctsAchievementId) =>
        Entry(AuditEventClass.DataChange, MilestoneAuditEvents.AchievementStarted, actorId, project, achievement,
        [
            AuditAttribute.Of(MilestoneAuditAttributes.ProjectMilestoneId, achievement.ProjectMilestoneId),
            AuditAttribute.Of(MilestoneAuditAttributes.RevisionNo, achievement.RevisionNo),
            AuditAttribute.Of(MilestoneAuditAttributes.ClaimedAchievementDate, achievement.ClaimedAchievementDate),
            AuditAttribute.Of(MilestoneAuditAttributes.CorrectsAchievementId, correctsAchievementId),
        ]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, ClaimInputs before, MilestoneAchievement achievement) =>
        Entry(AuditEventClass.DataChange, MilestoneAuditEvents.AchievementChanged, actorId, project, achievement,
            new[]
            {
                AuditAttribute.Change(MilestoneAuditAttributes.ClaimedAchievementDate, before.ClaimedAchievementDate, achievement.ClaimedAchievementDate),
                AuditAttribute.WithheldChange(MilestoneAuditAttributes.Narrative, before.Narrative?.Text, achievement.Narrative?.Text),
            }.OfType<AuditAttribute>());

    public static AuditEntry Deleted(Guid actorId, ProjectFacts project, MilestoneAchievement achievement) =>
        Entry(AuditEventClass.DataChange, MilestoneAuditEvents.AchievementDeleted, actorId, project, achievement,
        [
            AuditAttribute.Of(MilestoneAuditAttributes.ProjectMilestoneId, achievement.ProjectMilestoneId),
            AuditAttribute.Of(MilestoneAuditAttributes.RevisionNo, achievement.RevisionNo),
        ]);

    public static AuditEntry EvidenceAttached(Guid actorId, ProjectFacts project, MilestoneAchievement achievement, EvidenceReferenceDetail evidence) =>
        Entry(AuditEventClass.DataChange, MilestoneAuditEvents.EvidenceAttached, actorId, project, achievement, EvidenceAttributes(evidence));

    public static AuditEntry EvidenceWithdrawn(Guid actorId, ProjectFacts project, MilestoneAchievement achievement, EvidenceReferenceDetail evidence) =>
        Entry(AuditEventClass.DataChange, MilestoneAuditEvents.EvidenceWithdrawn, actorId, project, achievement, EvidenceAttributes(evidence));

    public static AuditEntry Submitted(Guid actorId, ProjectFacts project, MilestoneAchievement achievement, Guid approvalInstanceId, Guid? evidencePolicyVersionId) =>
        Transition(MilestoneAuditEvents.AchievementSubmitted, actorId, project, achievement, MilestoneAchievementStatus.Draft,
        [
            AuditAttribute.Of(MilestoneAuditAttributes.ApprovalInstanceId, approvalInstanceId),
            AuditAttribute.Of(MilestoneAuditAttributes.ClaimedAchievementDate, achievement.ClaimedAchievementDate),
            AuditAttribute.Of(MilestoneAuditAttributes.EvidencePolicyVersionId, evidencePolicyVersionId),
        ]);

    public static AuditEntry Accepted(ProjectFacts project, MilestoneAchievement achievement, ApprovalOutcomeRecorded outcome, Guid? supersededAchievementId) =>
        Transition(MilestoneAuditEvents.AchievementAccepted, outcome.Data.DecidedByUserId, project, achievement, MilestoneAchievementStatus.Submitted,
        [
            AuditAttribute.Of(MilestoneAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(MilestoneAuditAttributes.AcceptedActualAchievementDate, achievement.AcceptedActualAchievementDate),
            AuditAttribute.Of(MilestoneAuditAttributes.SupersededAchievementId, supersededAchievementId),
        ]);

    /// <summary>WF-11 returned, rejected or withdrew the revision, or approved it for a milestone cancelled meanwhile (<paramref name="reason"/>).</summary>
    public static AuditEntry Returned(ProjectFacts project, MilestoneAchievement achievement, ApprovalOutcomeRecorded outcome, string? reason) =>
        Transition(MilestoneAuditEvents.AchievementReturned, outcome.Data.DecidedByUserId, project, achievement, MilestoneAchievementStatus.Submitted,
        [
            AuditAttribute.Of(MilestoneAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(MilestoneAuditAttributes.Decision, outcome.Data.Decision),
            AuditAttribute.Of(MilestoneAuditAttributes.Reason, reason),
        ]);

    public static AuditEntry Superseded(Guid actorId, ProjectFacts project, MilestoneAchievement achievement) =>
        Transition(MilestoneAuditEvents.AchievementSuperseded, actorId, project, achievement, MilestoneAchievementStatus.Accepted,
            [AuditAttribute.Of(MilestoneAuditAttributes.SupersededByAchievementId, achievement.SupersededByAchievementId)]);

    public static AuditEntry OutcomeIgnored(ProjectFacts project, MilestoneAchievement achievement, ApprovalOutcomeRecorded outcome) =>
        new(AuditEventClass.LifecycleTransition, MilestoneAuditEvents.ApprovalOutcomeIgnored, AuditOutcome.Failed)
        {
            ActorUserId = outcome.Data.DecidedByUserId,
            Subject = SubjectOf(achievement),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(MilestoneAuditAttributes.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
                AuditAttribute.Of(MilestoneAuditAttributes.Decision, outcome.Data.Decision),
                AuditAttribute.Of(MilestoneAuditAttributes.SubjectRevisionNo, outcome.Subject.RevisionNo),
                AuditAttribute.Of(MilestoneAuditAttributes.Status, achievement.Status),
                AuditAttribute.Of(MilestoneAuditAttributes.RevisionNo, achievement.RevisionNo),
            ],
        };

    private static AuditAttribute[] EvidenceAttributes(EvidenceReferenceDetail evidence) =>
    [
        AuditAttribute.Of(MilestoneAuditAttributes.EvidenceReferenceId, evidence.Id),
        AuditAttribute.Of(MilestoneAuditAttributes.DocumentId, evidence.DocumentId),
        AuditAttribute.Of(MilestoneAuditAttributes.DocumentVersionId, evidence.DocumentVersionId),
        AuditAttribute.Of(MilestoneAuditAttributes.EvidenceTypeItemId, evidence.EvidenceTypeItemId),
    ];

    private static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, MilestoneAchievement achievement, MilestoneAchievementStatus from, IEnumerable<AuditAttribute> attributes) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, achievement,
        [
            AuditAttribute.Change(MilestoneAuditAttributes.Status, from, achievement.Status)!,
            AuditAttribute.Of(MilestoneAuditAttributes.ProjectMilestoneId, achievement.ProjectMilestoneId),
            AuditAttribute.Of(MilestoneAuditAttributes.RevisionNo, achievement.RevisionNo),
            .. attributes,
        ]);

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, MilestoneAchievement achievement, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = SubjectOf(achievement),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    private static AuditSubject SubjectOf(MilestoneAchievement achievement) =>
        new(MilestoneApprovalRouting.SubjectModule, MilestoneApprovalRouting.SubjectType, achievement.Id);
}

/// <summary>A DRAFT's claim before a change, for its audit event.</summary>
internal sealed record ClaimInputs(DateOnly ClaimedAchievementDate, NarrativeText? Narrative)
{
    public static ClaimInputs Of(MilestoneAchievement a) => new(a.ClaimedAchievementDate, a.Narrative);
}
