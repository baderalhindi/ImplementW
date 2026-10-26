using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Approval.Contracts.Events;
using PMPlatform.Application.Features.ManagementConcern.Contracts.Events;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ManagementConcern;
using A = PMPlatform.Application.Features.ManagementConcern.Contracts.Events.ConcernAuditAttributes;
using ConcernEntity = PMPlatform.Domain.ManagementConcern.ManagementConcern;

namespace PMPlatform.Application.Features.ManagementConcern;

/// <summary>
/// The audit events of WF-07 (TASK-033 <c>IAuditTrail</c>). Every event has the concern as subject — its escalations' too — so a
/// concern's history is one query. Titles, descriptions, resolutions and reasons are free text and are not copied.
/// </summary>
internal static class ConcernAudit
{
    public const string Module = "ManagementConcern";
    public const string ConcernType = "ManagementConcern";

    /// <summary>The concern as raised, with its computed severity and, when raised from a risk, the risk.</summary>
    public static AuditEntry Raised(Guid actorId, ProjectFacts project, ConcernEntity concern) =>
        Entry(AuditEventClass.DataChange, ConcernAuditEvents.ConcernRaised, actorId, AuditActorType.User, project, concern,
            new[]
            {
                AuditAttribute.Of(A.ConcernType, concern.ConcernType),
                AuditAttribute.Of(A.CategoryItemId, concern.CategoryItemId),
                AuditAttribute.Of(A.PriorityItemId, concern.PriorityItemId),
                AuditAttribute.Of(A.OverallImpactLevel, concern.OverallImpactLevel),
                AuditAttribute.Of(A.SeverityItemId, concern.SeverityItemId),
                AuditAttribute.Of(A.SeverityConfigurationVersionId, concern.SeverityConfigurationVersionId),
                AuditAttribute.Of(A.TargetResolutionDate, concern.TargetResolutionDate),
                AuditAttribute.Of(A.NextReviewDate, concern.NextReviewDate),
                AuditAttribute.Of(A.Status, concern.Status),
                concern.OriginatingRiskId is null ? null : AuditAttribute.Of(A.OriginatingRiskId, concern.OriginatingRiskId),
            }.OfType<AuditAttribute>());

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, ConcernFields before, ConcernEntity concern) =>
        Entry(AuditEventClass.DataChange, ConcernAuditEvents.ConcernChanged, actorId, AuditActorType.User, project, concern,
            new[]
            {
                AuditAttribute.WithheldChange(A.Title, before.Title.Text, concern.Title.Text),
                AuditAttribute.WithheldChange(A.Description, before.Description.Text, concern.Description.Text),
                AuditAttribute.Change(A.CategoryItemId, before.CategoryItemId, concern.CategoryItemId),
                AuditAttribute.Change(A.PriorityItemId, before.PriorityItemId, concern.PriorityItemId),
                AuditAttribute.Change(A.TargetResolutionDate, before.TargetResolutionDate, concern.TargetResolutionDate),
            }.OfType<AuditAttribute>());

    /// <summary>A reassessment: the severity before and after, and the version whose rule computed it.</summary>
    public static AuditEntry Assessed(Guid actorId, ProjectFacts project, ConcernSeverityFields before, ConcernEntity concern) =>
        Entry(AuditEventClass.DataChange, ConcernAuditEvents.ConcernAssessed, actorId, AuditActorType.User, project, concern,
            new[]
            {
                AuditAttribute.Change(A.OverallImpactLevel, before.OverallImpactLevel, concern.OverallImpactLevel) ?? AuditAttribute.Of(A.OverallImpactLevel, concern.OverallImpactLevel),
                AuditAttribute.Change(A.SeverityItemId, before.SeverityItemId, concern.SeverityItemId) ?? AuditAttribute.Of(A.SeverityItemId, concern.SeverityItemId),
                AuditAttribute.Of(A.SeverityConfigurationVersionId, concern.SeverityConfigurationVersionId),
            }.OfType<AuditAttribute>());

    /// <summary>A move along <see cref="ConcernWorkflow"/>, or a command that keeps the state, with what it set.</summary>
    public static AuditEntry Transition(string eventType, Guid actorId, ProjectFacts project, ConcernStatus from, ConcernEntity concern, Guid? approvalInstanceId = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, AuditActorType.User, project, concern,
            new[]
            {
                AuditAttribute.Change(A.Status, from, concern.Status) ?? AuditAttribute.Of(A.Status, concern.Status),
                AuditAttribute.Of(A.AssigneeUserId, concern.AssigneeUserId),
                AuditAttribute.Of(A.RevisionNo, concern.RevisionNo),
                concern.Resolution is null ? null : AuditAttribute.Of(A.Resolution, AuditAttribute.Withheld),
                approvalInstanceId is null ? null : AuditAttribute.Of(A.ApprovalInstanceId, approvalInstanceId),
            }.OfType<AuditAttribute>());

    public static AuditEntry Reviewed(Guid actorId, ProjectFacts project, DateOnly before, ConcernEntity concern) =>
        Entry(AuditEventClass.DataChange, ConcernAuditEvents.ConcernReviewed, actorId, AuditActorType.User, project, concern,
            [AuditAttribute.Change(A.NextReviewDate, before, concern.NextReviewDate) ?? AuditAttribute.Of(A.NextReviewDate, concern.NextReviewDate)]);

    /// <summary>WF-11's decision on a revision's validation, applied: the decider is the actor.</summary>
    public static AuditEntry Validated(string eventType, ProjectFacts project, ConcernStatus from, ConcernEntity concern, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        AuditEntry entry = Transition(eventType, outcome.Data.DecidedByUserId, project, from, concern, outcome.Data.ApprovalInstanceId);
        return entry with { Attributes = [.. entry.Attributes, AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision)] };
    }

    /// <summary>EV-5: an outcome for a revision no longer under validation, recorded by the service principal and not applied.</summary>
    public static AuditEntry OutcomeIgnored(ProjectFacts project, ConcernEntity concern, ApprovalOutcomeRecorded outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return Entry(AuditEventClass.DataChange, ConcernAuditEvents.OutcomeIgnored, outcome.Data.DecidedByUserId, AuditActorType.User, project, concern,
        [
            AuditAttribute.Of(A.ApprovalInstanceId, outcome.Data.ApprovalInstanceId),
            AuditAttribute.Of(A.ApprovalDecision, outcome.Data.Decision),
            AuditAttribute.Of(A.RevisionNo, outcome.Subject.RevisionNo),
            AuditAttribute.Of(A.Status, concern.Status),
        ]);
    }

    /// <summary>An escalation raised, resolved or withdrawn, on its concern.</summary>
    public static AuditEntry Escalation(string eventType, Guid actorId, ProjectFacts project, ConcernEntity concern, ConcernEscalation escalation) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, AuditActorType.User, project, concern,
            new[]
            {
                AuditAttribute.Of(A.EscalationId, escalation.Id),
                AuditAttribute.Of(A.EscalationNo, escalation.EscalationNo),
                AuditAttribute.Of(A.EscalatedToRoleId, escalation.EscalatedToRoleId),
                AuditAttribute.Of(A.Status, escalation.Status),
                AuditAttribute.Of(A.Reason, AuditAttribute.Withheld),
                escalation.Resolution is null ? null : AuditAttribute.Of(A.Resolution, AuditAttribute.Withheld),
            }.OfType<AuditAttribute>());

    /// <summary>ADR-013: management or escalation refused to an external user, whatever they hold.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, ProjectFacts project, Guid concernId, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, ConcernAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, ConcernType, concernId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.Reason, reason)],
        };

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, AuditActorType actorType, ProjectFacts project, ConcernEntity concern, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = new AuditSubject(Module, ConcernType, concern.Id),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };
}

/// <summary>A concern's own fields before a change, for its audit event.</summary>
internal sealed record ConcernFields(NarrativeText Title, NarrativeText Description, Guid CategoryItemId, Guid PriorityItemId, DateOnly? TargetResolutionDate)
{
    public static ConcernFields Of(ConcernEntity c) => new(c.Title, c.Description, c.CategoryItemId, c.PriorityItemId, c.TargetResolutionDate);
}

/// <summary>A concern's severity before a reassessment, for its audit event.</summary>
internal sealed record ConcernSeverityFields(short? OverallImpactLevel, Guid? SeverityItemId)
{
    public static ConcernSeverityFields Of(ConcernEntity c) => new(c.OverallImpactLevel, c.SeverityItemId);
}
