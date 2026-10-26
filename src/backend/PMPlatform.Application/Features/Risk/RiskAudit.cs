using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Application.Features.Risk.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.Risk;
using A = PMPlatform.Application.Features.Risk.Contracts.Events.RiskAuditAttributes;
using RiskEntity = PMPlatform.Domain.Risk.Risk;

namespace PMPlatform.Application.Features.Risk;

/// <summary>
/// The audit events of WF-06 (TASK-033 <c>IAuditTrail</c>). Every event of a risk, its assessments and its acceptances has the
/// risk as subject, so a risk's history is one query; an action's events have the action. Titles, descriptions, rationales and
/// reasons are free text and are not copied.
/// </summary>
internal static class RiskAudit
{
    public const string Module = "Risk";
    public const string RiskType = "Risk";
    public const string ActionType = "RiskTreatmentAction";

    public static AuditEntry Registered(Guid actorId, ProjectFacts project, RiskEntity risk) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.RiskRegistered, actorId, AuditActorType.User, project, SubjectOf(risk),
        [
            AuditAttribute.Of(A.RiskCategoryItemId, risk.RiskCategoryItemId),
            AuditAttribute.Of(A.OwnerUserId, risk.OwnerUserId),
            AuditAttribute.Of(A.IdentifiedDate, risk.IdentifiedDate),
            AuditAttribute.Of(A.NextReviewDate, risk.NextReviewDate),
            AuditAttribute.Of(A.Status, risk.Status),
        ]);

    public static AuditEntry Changed(Guid actorId, ProjectFacts project, RiskFields before, RiskEntity risk) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.RiskChanged, actorId, AuditActorType.User, project, SubjectOf(risk),
            new[]
            {
                AuditAttribute.WithheldChange(A.Title, before.Title.Text, risk.Title.Text),
                AuditAttribute.WithheldChange(A.Description, before.Description.Text, risk.Description.Text),
                AuditAttribute.Change(A.RiskCategoryItemId, before.RiskCategoryItemId, risk.RiskCategoryItemId),
                AuditAttribute.Change(A.OwnerUserId, before.OwnerUserId, risk.OwnerUserId),
                AuditAttribute.Change(A.IdentifiedDate, before.IdentifiedDate, risk.IdentifiedDate),
                AuditAttribute.Change(A.NextReviewDate, before.NextReviewDate, risk.NextReviewDate),
            }.OfType<AuditAttribute>());

    /// <summary>The assessment's pinned matrix version and rating, and the move to ASSESSED on a first assessment.</summary>
    public static AuditEntry Assessed(Guid actorId, ProjectFacts project, RiskStatus from, RiskEntity risk, RiskAssessmentVersion assessment, string ratingCode) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.RiskAssessed, actorId, AuditActorType.User, project, SubjectOf(risk),
            new[]
            {
                AuditAttribute.Change(A.Status, from, risk.Status),
                AuditAttribute.Of(A.AssessmentVersionNo, assessment.VersionNo),
                AuditAttribute.Of(A.MatrixConfigurationVersionId, assessment.MatrixConfigurationVersionId),
                AuditAttribute.Of(A.ProbabilityLevel, assessment.ProbabilityLevel),
                AuditAttribute.Of(A.OverallImpactLevel, assessment.OverallImpactLevel),
                AuditAttribute.Of(A.RiskRatingDefinitionId, assessment.RiskRatingDefinitionId),
                AuditAttribute.Of(A.RatingCode, ratingCode),
            }.OfType<AuditAttribute>());

    /// <summary>A move along <see cref="RiskWorkflow"/>, with the counters and dates it set and, for an acceptance, which one.</summary>
    public static AuditEntry Transition(
        string eventType, Guid actorId, AuditActorType actorType, ProjectFacts project, RiskStatus from, RiskEntity risk, RiskAcceptance? acceptance = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, actorType, project, SubjectOf(risk),
            new[]
            {
                AuditAttribute.Change(A.Status, from, risk.Status) ?? AuditAttribute.Of(A.Status, risk.Status),
                AuditAttribute.Of(A.ReopenedCount, risk.ReopenedCount),
                AuditAttribute.Of(A.NextReviewDate, risk.NextReviewDate),
                risk.ClosureRationale is null ? null : AuditAttribute.Of(A.ClosureRationale, AuditAttribute.Withheld),
                acceptance is null ? null : AuditAttribute.Of(A.AcceptanceId, acceptance.Id),
                acceptance is null ? null : AuditAttribute.Of(A.ExpiresOn, acceptance.ExpiresOn),
            }.OfType<AuditAttribute>());

    public static AuditEntry Materialised(Guid actorId, ProjectFacts project, RiskEntity risk, Guid concernId) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.RiskMaterialised, actorId, AuditActorType.User, project, SubjectOf(risk),
            [AuditAttribute.Of(A.ManagementConcernId, concernId), AuditAttribute.Of(A.Status, risk.Status)]);

    /// <summary>ADR-013: an assessment or acceptance refused to an external user, whatever they hold.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, ProjectFacts project, Guid riskId, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, RiskAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, RiskType, riskId),
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.Reason, reason)],
        };

    public static AuditEntry ActionCreated(Guid actorId, ProjectFacts project, RiskTreatmentAction action) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.TreatmentActionCreated, actorId, AuditActorType.User, project, SubjectOf(action),
        [
            AuditAttribute.Of(A.ActionType, action.ActionType),
            AuditAttribute.Of(A.OwnerUserId, action.OwnerUserId),
            AuditAttribute.Of(A.DueDate, action.DueDate),
            AuditAttribute.Of(A.Status, action.Status),
        ]);

    public static AuditEntry ActionChanged(Guid actorId, ProjectFacts project, ActionPlan before, RiskTreatmentAction action) =>
        Entry(AuditEventClass.DataChange, RiskAuditEvents.TreatmentActionChanged, actorId, AuditActorType.User, project, SubjectOf(action),
            new[]
            {
                AuditAttribute.WithheldChange(A.Title, before.Title.Text, action.Title.Text),
                AuditAttribute.WithheldChange(A.Description, before.Description?.Text, action.Description?.Text),
                AuditAttribute.Change(A.ActionType, before.ActionType, action.ActionType),
                AuditAttribute.Change(A.OwnerUserId, before.OwnerUserId, action.OwnerUserId),
                AuditAttribute.Change(A.DueDate, before.DueDate, action.DueDate),
            }.OfType<AuditAttribute>());

    public static AuditEntry ActionTransition(string eventType, Guid actorId, ProjectFacts project, RiskTreatmentActionStatus from, RiskTreatmentAction action) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, AuditActorType.User, project, SubjectOf(action),
            [AuditAttribute.Change(A.Status, from, action.Status)!]);

    private static AuditEntry Entry(
        AuditEventClass eventClass, string eventType, Guid actorId, AuditActorType actorType, ProjectFacts project, AuditSubject subject, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            ActorType = actorType,
            Subject = subject,
            ScopeProjectId = project.Id,
            ScopeExternalEntityId = project.ExternalEntityId,
            Attributes = [.. attributes],
        };

    private static AuditSubject SubjectOf(RiskEntity risk) => new(Module, RiskType, risk.Id);

    private static AuditSubject SubjectOf(RiskTreatmentAction action) => new(Module, ActionType, action.Id);
}

/// <summary>A risk's register fields before a change, for its audit event.</summary>
internal sealed record RiskFields(
    NarrativeText Title, NarrativeText Description, Guid RiskCategoryItemId, Guid? OwnerUserId, DateOnly IdentifiedDate, DateOnly? NextReviewDate)
{
    public static RiskFields Of(RiskEntity r) => new(r.Title, r.Description, r.RiskCategoryItemId, r.OwnerUserId, r.IdentifiedDate, r.NextReviewDate);
}

/// <summary>An action's plan before a change, for its audit event.</summary>
internal sealed record ActionPlan(NarrativeText Title, NarrativeText? Description, RiskTreatmentActionType ActionType, Guid? OwnerUserId, DateOnly? DueDate)
{
    public static ActionPlan Of(RiskTreatmentAction a) => new(a.Title, a.Description, a.ActionType, a.OwnerUserId, a.DueDate);
}
