using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.Progress.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>The audit events of WF-02 (TASK-033 <c>IAuditTrail</c>). Narratives and reasons are free text and are not copied.</summary>
internal static class ProgressAudit
{
    public const string Module = "Progress";
    public const string SubmissionType = "ProgressSubmission";
    public const string CycleType = "ReportingCycle";
    public const string HealthType = "ProjectHealthStatus";

    public static AuditEntry CycleCreated(Guid actorId, ProjectFacts project, ReportingCycle cycle) =>
        Entry(AuditEventClass.DataChange, ProgressAuditEvents.ReportingCycleCreated, actorId, project, new AuditSubject(Module, CycleType, cycle.Id),
        [
            AuditAttribute.Of(ProgressAuditAttributes.PeriodStart, cycle.PeriodStart),
            AuditAttribute.Of(ProgressAuditAttributes.PeriodEnd, cycle.PeriodEnd),
            AuditAttribute.Of(ProgressAuditAttributes.Status, cycle.Status),
        ]);

    public static AuditEntry Started(Guid actorId, ProjectFacts project, ProgressSubmission submission) =>
        Entry(AuditEventClass.DataChange, ProgressAuditEvents.ProgressUpdateStarted, actorId, project, SubjectOf(submission),
        [
            AuditAttribute.Of(ProgressAuditAttributes.Status, submission.Status),
            AuditAttribute.Of(ProgressAuditAttributes.ReportingCycleId, submission.ReportingCycleId),
            AuditAttribute.Of(ProgressAuditAttributes.RevisionNo, submission.RevisionNo),
            .. Figures(submission),
        ]);

    public static AuditEntry OpeningPositionRecorded(Guid actorId, ProjectFacts project, ProgressSubmission submission) =>
        Entry(AuditEventClass.DataChange, ProgressAuditEvents.OpeningPositionRecorded, actorId, project, SubjectOf(submission),
        [
            AuditAttribute.Of(ProgressAuditAttributes.Status, submission.Status),
            AuditAttribute.Of(ProgressAuditAttributes.ReportingCycleId, submission.ReportingCycleId),
            AuditAttribute.Of(ProgressAuditAttributes.ProjectIntakeId, submission.ProjectIntakeId),
            AuditAttribute.Of(ProgressAuditAttributes.ActualPercentCalculated, submission.ActualPercentCalculated),
        ]);

    /// <summary>What a person wrote on the DRAFT: the override's figure, and that the narrative or a reason changed.</summary>
    public static AuditEntry Changed(Guid actorId, ProjectFacts project, ProgressSubmission submission, NarrativeText? narrativeBefore, decimal? overrideBefore, NarrativeText? reasonBefore) =>
        Entry(AuditEventClass.DataChange, ProgressAuditEvents.ProgressUpdateChanged, actorId, project, SubjectOf(submission),
            new[]
            {
                AuditAttribute.WithheldChange(ProgressAuditAttributes.Narrative, narrativeBefore?.Text, submission.Narrative?.Text),
                AuditAttribute.Change(ProgressAuditAttributes.ActualPercentOverride, overrideBefore, submission.ActualPercentOverride),
                AuditAttribute.WithheldChange(ProgressAuditAttributes.OverrideReason, reasonBefore?.Text, submission.OverrideReason?.Text),
            }.OfType<AuditAttribute>());

    public static AuditEntry Submitted(Guid actorId, ProjectFacts project, ProgressSubmission submission) =>
        Transition(ProgressAuditEvents.ProgressSubmitted, actorId, project, submission, ProgressSubmissionStatus.Draft, Figures(submission));

    public static AuditEntry ReviewStarted(Guid actorId, ProjectFacts project, ProgressSubmission submission) =>
        Transition(ProgressAuditEvents.ReviewStarted, actorId, project, submission, ProgressSubmissionStatus.Submitted, []);

    public static AuditEntry Returned(Guid actorId, ProjectFacts project, ProgressSubmission submission) =>
        Transition(ProgressAuditEvents.ProgressReturned, actorId, project, submission, ProgressSubmissionStatus.UnderReview,
            [AuditAttribute.WithheldChange(ProgressAuditAttributes.ReturnReason, null, submission.ReturnReason?.Text)!]);

    public static AuditEntry Published(Guid actorId, ProjectFacts project, ProgressSubmission submission, PublishedProgressSnapshot snapshot) =>
        Transition(ProgressAuditEvents.ProgressPublished, actorId, project, submission, ProgressSubmissionStatus.UnderReview,
        [
            AuditAttribute.Of(ProgressAuditAttributes.SnapshotId, snapshot.Id),
            AuditAttribute.Of(ProgressAuditAttributes.ActualPercent, snapshot.ActualPercent),
            AuditAttribute.Of(ProgressAuditAttributes.PlannedPercent, snapshot.PlannedPercent),
            AuditAttribute.Of(ProgressAuditAttributes.OverallHealth, snapshot.OverallHealth),
            AuditAttribute.Of(ProgressAuditAttributes.HealthRuleConfigurationVersionId, snapshot.HealthRuleConfigurationVersionId),
        ]);

    public static AuditEntry HealthRecomputed(Guid actorId, ProjectFacts project, ProjectHealthStatus health, HealthStatus? before) =>
        Entry(AuditEventClass.DataChange, ProgressAuditEvents.ProjectHealthRecomputed, actorId, project, new AuditSubject(Module, HealthType, health.Id),
        [
            AuditAttribute.Change(ProgressAuditAttributes.OverallHealth, before, health.OverallHealth) ?? AuditAttribute.Of(ProgressAuditAttributes.OverallHealth, health.OverallHealth),
            AuditAttribute.Of(ProgressAuditAttributes.ActualPercent, health.ActualPercent),
            AuditAttribute.Of(ProgressAuditAttributes.PlannedPercent, health.PlannedPercent),
            AuditAttribute.Of(ProgressAuditAttributes.HealthRuleConfigurationVersionId, health.HealthRuleConfigurationVersionId),
        ]);

    public static AuditEntry ReviewRefused(Guid actorId, ProjectFacts project, ProgressSubmission submission, string reason) =>
        new(AuditEventClass.AuthorizationDenial, ProgressAuditEvents.ReviewRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = SubjectOf(submission),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes =
            [
                AuditAttribute.Of(ProgressAuditAttributes.Gate, PermissionCatalogue.ProgressReview),
                AuditAttribute.Of(ProgressAuditAttributes.Status, submission.Status),
                AuditAttribute.Of(ProgressAuditAttributes.Reason, reason),
            ],
        };

    private static AuditAttribute[] Figures(ProgressSubmission submission) =>
    [
        AuditAttribute.Of(ProgressAuditAttributes.ActualPercentCalculated, submission.ActualPercentCalculated),
        AuditAttribute.Of(ProgressAuditAttributes.ActualPercentOverride, submission.ActualPercentOverride),
        AuditAttribute.Of(ProgressAuditAttributes.PlannedPercent, submission.PlannedPercent),
        AuditAttribute.Of(ProgressAuditAttributes.BaselineId, submission.BaselineId),
    ];

    private static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, ProgressSubmission submission, ProgressSubmissionStatus from, IEnumerable<AuditAttribute> attributes) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, SubjectOf(submission),
        [
            AuditAttribute.Change(ProgressAuditAttributes.Status, from, submission.Status)!,
            AuditAttribute.Of(ProgressAuditAttributes.RevisionNo, submission.RevisionNo),
            .. attributes,
        ]);

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, AuditSubject subject, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    private static AuditSubject SubjectOf(ProgressSubmission submission) => new(Module, SubmissionType, submission.Id);
}
