using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.Closure.Contracts;
using PMPlatform.Application.Features.Closure.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Closure;
using PMPlatform.Domain.Common;
using A = PMPlatform.Application.Features.Closure.Contracts.Events.ClosureAuditAttributes;

namespace PMPlatform.Application.Features.Closure;

/// <summary>
/// The audit events of WF-10 (TASK-063 <c>IAuditTrail</c>; CLO-CC-24). A case's events have the case as subject, so its history — raised,
/// evaluated, waived, decided, effected — is one query; an obligation's have the obligation. Narratives, titles and reasons are free text
/// and are not copied.
/// </summary>
internal static class CloseoutAudit
{
    public static AuditSubject SubjectOf(CloseoutCase @case) => @case switch
    {
        CompletionCase => new AuditSubject(ClosureApprovalRouting.SubjectModule, ClosureApprovalRouting.CompletionSubjectType, @case.Id),
        ClosureCase => new AuditSubject(ClosureApprovalRouting.SubjectModule, ClosureApprovalRouting.ClosureSubjectType, @case.Id),
        _ => throw new ArgumentOutOfRangeException(nameof(@case), @case, "Unknown case."),
    };

    public static AuditSubject SubjectOf(PostProjectObligation obligation)
    {
        ArgumentNullException.ThrowIfNull(obligation);
        return new AuditSubject(ClosureApprovalRouting.SubjectModule, "PostProjectObligation", obligation.Id);
    }

    public static AuditEntry Created(Guid actorId, ProjectFacts project, CloseoutCase @case) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.CaseCreated, actorId, project, SubjectOf(@case),
        [
            AuditAttribute.Of(A.Status, @case.Status),
            @case is ClosureCase closure ? AuditAttribute.Of(A.Outcome, closure.Outcome) : null,
            @case is ClosureCase { CompletionCaseId: { } completionId } ? AuditAttribute.Of(A.CompletionCaseId, completionId) : null,
            .. Fields(null, @case),
        ]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, CloseoutFields before, CloseoutCase @case) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.CaseChanged, actorId, project, SubjectOf(@case), Fields(before, @case));

    public static AuditEntry Deleted(Guid actorId, ProjectFacts project, CloseoutCase @case) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.CaseDeleted, actorId, project, SubjectOf(@case), [AuditAttribute.Of(A.Status, @case.Status)]);

    public static AuditEntry ReadinessEvaluated(Guid actorId, ProjectFacts project, CloseoutCase @case, ReadinessDetail readiness) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.ReadinessEvaluated, actorId, project, SubjectOf(@case),
            [AuditAttribute.Of(A.Readiness, readiness.Status), AuditAttribute.Of(A.RevisionNo, @case.RevisionNo)]);

    public static AuditEntry CheckWaived(Guid actorId, ProjectFacts project, CloseoutCase @case, ReadinessCheck waiver) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.CheckWaived, actorId, project, SubjectOf(@case),
        [
            AuditAttribute.Of(A.CheckCode, waiver.CheckCode),
            AuditAttribute.Of(A.BlockingCount, waiver.BlockingCount),
            AuditAttribute.Of(A.Reason, AuditAttribute.Withheld),
        ]);

    /// <summary>A move along <see cref="CloseoutWorkflow"/>, with the revision and, for a submission, its readiness; for a review, its WF-11 run.</summary>
    public static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, CloseoutCaseStatus from, CloseoutCase @case, ReadinessDetail? readiness = null, Guid? approvalInstanceId = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, SubjectOf(@case),
        [
            AuditAttribute.Change(A.Status, from, @case.Status) ?? AuditAttribute.Of(A.Status, @case.Status),
            AuditAttribute.Of(A.RevisionNo, @case.RevisionNo),
            readiness is null ? null : AuditAttribute.Of(A.Readiness, readiness.Status),
            approvalInstanceId is null ? null : AuditAttribute.Of(A.ApprovalInstanceId, approvalInstanceId),
        ]);

    /// <summary>WF-11's decision on a revision, applied: the decider is the actor. APPROVED changes no project.</summary>
    public static AuditEntry Decided(string eventType, ProjectFacts project, CloseoutCaseStatus from, CloseoutCase @case, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        AuditEntry entry = Transition(eventType, outcome.Data.DecidedByUserId, project, from, @case, approvalInstanceId: outcome.Data.ApprovalInstanceId);
        return entry with { Attributes = [.. entry.Attributes, AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision)] };
    }

    /// <summary>
    /// APPROVED → EFFECTED: the lifecycle activation, by a person or by WF-10's service principal, with the readiness it was revalidated
    /// against and, for a completion, the official completion date; for a closure, the outcome. Its own event, in its own transaction.
    /// </summary>
    public static AuditEntry Effected(Guid actorId, AuditActorType actorType, ProjectFacts project, CloseoutCase @case, ReadinessDetail readiness)
    {
        AuditEntry entry = Transition(ClosureAuditEvents.CaseEffected, actorId, project, CloseoutCaseStatus.Approved, @case, readiness);
        return entry with
        {
            ActorType = actorType,
            Attributes =
            [
                .. entry.Attributes,
                AuditAttribute.Of(A.EffectedAt, @case.EffectedAt),
                .. new[]
                {
                    @case is CompletionCase completion ? AuditAttribute.Of(A.ActualProjectCompletionDate, completion.ActualProjectCompletionDate) : null,
                    @case is ClosureCase closure ? AuditAttribute.Of(A.Outcome, closure.Outcome) : null,
                }.OfType<AuditAttribute>(),
            ],
        };
    }

    /// <summary>EV-5: an outcome for a revision no longer under review, recorded and not applied.</summary>
    public static AuditEntry OutcomeIgnored(ProjectFacts project, CloseoutCase @case, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return Entry(AuditEventClass.DataChange, ClosureAuditEvents.OutcomeIgnored, outcome.Data.DecidedByUserId, project, SubjectOf(@case),
        [
            AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision),
            AuditAttribute.Of(A.RevisionNo, outcome.Subject.RevisionNo),
            AuditAttribute.Of(A.Status, @case.Status),
        ]);
    }

    /// <summary>ADR-013: review, waiver or activation refused to an external user, whatever they hold.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, ProjectFacts project, AuditSubject subject, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, ClosureAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.RefusalReason, reason)],
        };

    public static AuditEntry ObligationCreated(Guid actorId, ProjectFacts project, PostProjectObligation obligation) =>
        Entry(AuditEventClass.DataChange, ClosureAuditEvents.ObligationCreated, actorId, project, SubjectOf(obligation),
        [
            AuditAttribute.Of(A.Status, obligation.Status),
            AuditAttribute.Of(A.Title, AuditAttribute.Withheld),
            .. ObligationFields(null, obligation),
        ]);

    public static AuditEntry ObligationChanged(Guid actorId, ProjectFacts project, PostProjectObligationChanges before, PostProjectObligation obligation)
    {
        ArgumentNullException.ThrowIfNull(before);
        return Entry(AuditEventClass.DataChange, ClosureAuditEvents.ObligationChanged, actorId, project, SubjectOf(obligation),
        [
            AuditAttribute.WithheldChange(A.Title, before.Title.Text, obligation.Title.Text),
            AuditAttribute.WithheldChange(A.Description, before.Description?.Text, obligation.Description?.Text),
            .. ObligationFields(before, obligation),
        ]);
    }

    public static AuditEntry ObligationTransitioned(Guid actorId, ProjectFacts project, PostProjectObligationStatus from, PostProjectObligation obligation) =>
        Entry(AuditEventClass.LifecycleTransition, ClosureAuditEvents.ObligationTransitioned, actorId, project, SubjectOf(obligation),
            [AuditAttribute.Change(A.Status, from, obligation.Status)]);

    private static IEnumerable<AuditAttribute?> ObligationFields(PostProjectObligationChanges? before, PostProjectObligation after) =>
    [
        AuditAttribute.Change(A.OwnerUserId, before?.OwnerUserId, after.OwnerUserId),
        AuditAttribute.Change(A.DueDate, before?.DueDate, after.DueDate),
    ];

    private static IEnumerable<AuditAttribute?> Fields(CloseoutFields? before, CloseoutCase after)
    {
        CloseoutFields now = CloseoutFields.Of(after);
        return
        [
            AuditAttribute.Change(A.ActualProjectCompletionDate, before?.ActualProjectCompletionDate, now.ActualProjectCompletionDate),
            AuditAttribute.WithheldChange(A.Narrative, before?.Narrative?.Text, now.Narrative?.Text),
        ];
    }

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, AuditSubject subject, IEnumerable<AuditAttribute?> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = AuditActorType.User,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes.OfType<AuditAttribute>()],
        };
}

/// <summary>A case's own fields as they were, for the record of what an edit changed.</summary>
internal sealed record CloseoutFields(DateOnly? ActualProjectCompletionDate, NarrativeText? Narrative)
{
    public static CloseoutFields Of(CloseoutCase @case) => @case switch
    {
        CompletionCase c => new CloseoutFields(c.ActualProjectCompletionDate, c.CompletionNarrative),
        ClosureCase c => new CloseoutFields(null, c.ClosureNarrative),
        _ => throw new ArgumentOutOfRangeException(nameof(@case), @case, "Unknown case."),
    };
}
