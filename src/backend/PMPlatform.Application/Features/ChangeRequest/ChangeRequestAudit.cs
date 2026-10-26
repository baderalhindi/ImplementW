using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ChangeRequest.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ChangeRequest;
using PMPlatform.Domain.Common;
using A = PMPlatform.Application.Features.ChangeRequest.Contracts.Events.ChangeRequestAuditAttributes;
using ChangeRequestEntity = PMPlatform.Domain.ChangeRequest.ChangeRequest;

namespace PMPlatform.Application.Features.ChangeRequest;

/// <summary>
/// The audit events of WF-08 (TASK-060 <c>IAuditTrail</c>). Every event has the change request as subject — its evaluations' and
/// authorisations' too — so a request's history, from raising to the application of each authorisation, is one query. Titles,
/// justifications and scope impacts are free text and are not copied.
/// </summary>
internal static class ChangeRequestAudit
{
    public const string Module = "ChangeRequest";
    public const string ChangeRequestType = "ChangeRequest";

    public static AuditEntry Created(Guid actorId, ProjectFacts project, ChangeRequestEntity request) =>
        Entry(AuditEventClass.DataChange, ChangeRequestAuditEvents.ChangeRequestCreated, actorId, project, request,
            [AuditAttribute.Of(A.ChangeType, request.ChangeType), AuditAttribute.Of(A.Status, request.Status), .. Fields(ChangeRequestFields.Of(request))]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, ChangeRequestFields before, ChangeRequestEntity request)
    {
        ArgumentNullException.ThrowIfNull(before);
        ChangeRequestFields after = ChangeRequestFields.Of(request);
        return Entry(AuditEventClass.DataChange, ChangeRequestAuditEvents.ChangeRequestChanged, actorId, project, request,
            new[]
            {
                AuditAttribute.WithheldChange(A.Title, before.Title.Text, after.Title.Text),
                AuditAttribute.WithheldChange(A.Justification, before.Justification.Text, after.Justification.Text),
                AuditAttribute.WithheldChange(A.ScopeImpact, before.ScopeImpact?.Text, after.ScopeImpact?.Text),
                AuditAttribute.Change(A.CostImpactSar, before.CostImpactSar, after.CostImpactSar),
                AuditAttribute.Change(A.ScheduleImpactDays, before.ScheduleImpactDays, after.ScheduleImpactDays),
                AuditAttribute.Change(A.IsContractualObligation, before.IsContractualObligation, after.IsContractualObligation),
                AuditAttribute.Change(A.RequestedGovernanceProfileItemId, before.RequestedGovernanceProfileItemId, after.RequestedGovernanceProfileItemId),
            }.OfType<AuditAttribute>());
    }

    public static AuditEntry Deleted(Guid actorId, ProjectFacts project, ChangeRequestEntity request) =>
        Entry(AuditEventClass.DataChange, ChangeRequestAuditEvents.ChangeRequestDeleted, actorId, project, request,
            [AuditAttribute.Of(A.ChangeType, request.ChangeType), AuditAttribute.Of(A.Status, request.Status)]);

    /// <summary>A move along <see cref="ChangeRequestWorkflow"/>, with the revision and, for a review, its WF-11 run and band.</summary>
    public static AuditEntry Transition(
        string eventType, Guid actorId, ProjectFacts project, ChangeRequestStatus from, ChangeRequestEntity request, Guid? approvalInstanceId = null, short? bandNo = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, request,
            new[]
            {
                AuditAttribute.Change(A.Status, from, request.Status) ?? AuditAttribute.Of(A.Status, request.Status),
                AuditAttribute.Of(A.RevisionNo, request.RevisionNo),
                approvalInstanceId is null ? null : AuditAttribute.Of(A.ApprovalInstanceId, approvalInstanceId),
                bandNo is null ? null : AuditAttribute.Of(A.ResultingBandNo, bandNo),
            }.OfType<AuditAttribute>());

    /// <summary>The authoritative evaluation of a revision: its bands, its cumulative position and what it was evaluated against.</summary>
    public static AuditEntry Evaluated(Guid actorId, ProjectFacts project, ChangeRequestEntity request, MaterialityEvaluation evaluation) =>
        Entry(AuditEventClass.DataChange, ChangeRequestAuditEvents.MaterialityEvaluated, actorId, project, request,
            new[]
            {
                AuditAttribute.Of(A.MaterialityEvaluationId, evaluation.Id),
                AuditAttribute.Of(A.RevisionNo, evaluation.RevisionNo),
                AuditAttribute.Of(A.MaterialityConfigurationVersionId, evaluation.MaterialityConfigurationVersionId),
                evaluation.ProjectBaselineId is null ? null : AuditAttribute.Of(A.ProjectBaselineId, evaluation.ProjectBaselineId),
                evaluation.FinancialCommitmentId is null ? null : AuditAttribute.Of(A.FinancialCommitmentId, evaluation.FinancialCommitmentId),
                AuditAttribute.Of(A.CumulativeCostImpactSar, evaluation.CumulativeCostImpactSar),
                AuditAttribute.Of(A.CumulativeScheduleImpactDays, evaluation.CumulativeScheduleImpactDays),
                evaluation.CostBandNo is null ? null : AuditAttribute.Of(A.CostBandNo, evaluation.CostBandNo),
                evaluation.ScheduleBandNo is null ? null : AuditAttribute.Of(A.ScheduleBandNo, evaluation.ScheduleBandNo),
                evaluation.ScopeBandNo is null ? null : AuditAttribute.Of(A.ScopeBandNo, evaluation.ScopeBandNo),
                AuditAttribute.Of(A.ResultingBandNo, evaluation.ResultingBandNo),
            }.OfType<AuditAttribute>());

    /// <summary>WF-11's decision on a revision, applied: the decider is the actor.</summary>
    public static AuditEntry Decided(string eventType, ProjectFacts project, ChangeRequestStatus from, ChangeRequestEntity request, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        AuditEntry entry = Transition(eventType, outcome.Data.DecidedByUserId, project, from, request, outcome.Data.ApprovalInstanceId);
        return entry with { Attributes = [.. entry.Attributes, AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision)] };
    }

    /// <summary>EV-5: an outcome for a revision no longer under review, recorded and not applied.</summary>
    public static AuditEntry OutcomeIgnored(ProjectFacts project, ChangeRequestEntity request, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return Entry(AuditEventClass.DataChange, ChangeRequestAuditEvents.OutcomeIgnored, outcome.Data.DecidedByUserId, project, request,
        [
            AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision),
            AuditAttribute.Of(A.RevisionNo, outcome.Subject.RevisionNo),
            AuditAttribute.Of(A.Status, request.Status),
        ]);
    }

    /// <summary>An authorisation issued by the approval, or applied by its target module, with what it permits.</summary>
    public static AuditEntry Authorization(string eventType, Guid actorId, ProjectFacts project, ChangeRequestEntity request, ChangeAuthorization authorization) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, project, request,
            new[]
            {
                AuditAttribute.Of(A.ChangeAuthorizationId, authorization.Id),
                AuditAttribute.Of(A.AuthorizationScope, authorization.AuthorizationScope),
                AuditAttribute.Of(A.TargetModule, authorization.TargetModule),
                AuditAttribute.Of(A.TargetType, authorization.TargetType),
                AuditAttribute.Of(A.TargetId, authorization.TargetId),
                AuditAttribute.Of(A.TargetRevisionNo, authorization.TargetRevisionNo),
                AuditAttribute.Of(A.Status, authorization.Status),
                AuditAttribute.Of(A.ApprovalInstanceId, authorization.ApprovalInstanceId),
                authorization.AppliedReference is null ? null : AuditAttribute.Of(A.AppliedReference, authorization.AppliedReference),
            }.OfType<AuditAttribute>());

    /// <summary>ADR-013: review or implementation refused to an external user, whatever they hold.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, ProjectFacts project, Guid changeRequestId, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, ChangeRequestAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, ChangeRequestType, changeRequestId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.Reason, reason)],
        };

    private static IEnumerable<AuditAttribute> Fields(ChangeRequestFields fields) =>
        new[]
        {
            AuditAttribute.Of(A.Title, AuditAttribute.Withheld),
            AuditAttribute.Of(A.Justification, AuditAttribute.Withheld),
            fields.ScopeImpact is null ? null : AuditAttribute.Of(A.ScopeImpact, AuditAttribute.Withheld),
            fields.CostImpactSar is null ? null : AuditAttribute.Of(A.CostImpactSar, fields.CostImpactSar),
            fields.ScheduleImpactDays is null ? null : AuditAttribute.Of(A.ScheduleImpactDays, fields.ScheduleImpactDays),
            AuditAttribute.Of(A.IsContractualObligation, fields.IsContractualObligation),
            fields.RequestedGovernanceProfileItemId is null ? null : AuditAttribute.Of(A.RequestedGovernanceProfileItemId, fields.RequestedGovernanceProfileItemId),
        }.OfType<AuditAttribute>();

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, ProjectFacts project, ChangeRequestEntity request, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = AuditActorType.User,
            Subject = new AuditSubject(Module, ChangeRequestType, request.Id),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };
}

/// <summary>A request's own fields before a change, for its audit event.</summary>
internal sealed record ChangeRequestFields(
    NarrativeText Title,
    NarrativeText Justification,
    Money? CostImpactSar,
    int? ScheduleImpactDays,
    NarrativeText? ScopeImpact,
    bool IsContractualObligation,
    Guid? RequestedGovernanceProfileItemId)
{
    public static ChangeRequestFields Of(ChangeRequestEntity r) =>
        new(r.Title, r.Justification, r.CostImpactSar, r.ScheduleImpactDays, r.ScopeImpact, r.IsContractualObligation, r.RequestedGovernanceProfileItemId);
}
