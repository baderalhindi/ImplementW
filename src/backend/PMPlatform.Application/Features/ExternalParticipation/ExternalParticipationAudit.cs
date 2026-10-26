using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Features.ExternalParticipation.Contracts.Events;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.ExternalParticipation;
using A = PMPlatform.Application.Features.ExternalParticipation.Contracts.Events.ExternalParticipationAuditAttributes;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The audit events of WF-13 (TASK-033 <c>IAuditTrail</c>; WF-13 §23.2). Every event has the request as subject — its revisions' and
/// application attempts' too — and the request's entity as scope, so a request's history is one query and an entity's activity is
/// another. Instructions, answers, reasons and notes are free text and are not copied.
/// </summary>
internal static class ExternalParticipationAudit
{
    public const string Module = "ExternalParticipation";
    public const string RequestType = "ExternalUpdateRequest";

    public static AuditEntry RequestCreated(Guid actorId, ExternalUpdateRequest request) =>
        Entry(AuditEventClass.DataChange, ExternalParticipationAuditEvents.RequestCreated, actorId, request, RequestAttributes(request));

    public static AuditEntry RequestChanged(Guid actorId, RequestFields before, ExternalUpdateRequest request) =>
        Entry(AuditEventClass.DataChange, ExternalParticipationAuditEvents.RequestChanged, actorId, request,
            new[]
            {
                AuditAttribute.Change(A.ContributionTypeItemId, before.ContributionTypeItemId, request.ContributionTypeItemId),
                AuditAttribute.Change(A.TargetId, before.TargetId, request.TargetId),
                AuditAttribute.WithheldChange(A.Instructions, before.Instructions.Text, request.Instructions.Text),
                AuditAttribute.Change(A.ResponsibleUserId, before.ResponsibleUserId, request.ResponsibleUserId),
                AuditAttribute.Change(A.ReviewerUserId, before.ReviewerUserId, request.ReviewerUserId),
                AuditAttribute.Change(A.DueDate, before.DueDate, request.DueDate),
            }.OfType<AuditAttribute>());

    public static AuditEntry RequestDeleted(Guid actorId, ExternalUpdateRequest request) =>
        Entry(AuditEventClass.DataChange, ExternalParticipationAuditEvents.RequestDeleted, actorId, request, [AuditAttribute.Of(A.Status, request.Status)]);

    /// <summary>A request's own move along its state machine: issued or cancelled.</summary>
    public static AuditEntry RequestTransition(string eventType, Guid actorId, ExternalUpdateRequestStatus from, ExternalUpdateRequest request) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, request,
            new[]
            {
                AuditAttribute.Change(A.Status, from, request.Status),
                AuditAttribute.Of(A.ResponsibleUserId, request.ResponsibleUserId),
                AuditAttribute.Of(A.ReviewerUserId, request.ReviewerUserId),
                AuditAttribute.Of(A.DueDate, request.DueDate),
                request.CancellationReason is null ? null : AuditAttribute.Of(A.Reason, AuditAttribute.Withheld),
            }.OfType<AuditAttribute>());

    /// <summary>A responder or reviewer replaced.</summary>
    public static AuditEntry Assigned(string eventType, string attribute, Guid actorId, Guid? before, Guid after, ExternalUpdateRequest request) =>
        Entry(AuditEventClass.DataChange, eventType, actorId, request, [AuditAttribute.Change(attribute, before, after) ?? AuditAttribute.Of(attribute, after)]);

    /// <summary>A revision drafted, or its values replaced: which fields it holds, not what they say.</summary>
    public static AuditEntry ContributionValues(string eventType, Guid actorId, ExternalUpdateRequest request, ExternalContribution contribution, IEnumerable<string> fieldCodes) =>
        Entry(AuditEventClass.DataChange, eventType, actorId, request,
        [
            AuditAttribute.Of(A.ExternalContributionId, contribution.Id),
            AuditAttribute.Of(A.RevisionNo, contribution.RevisionNo),
            AuditAttribute.Of(A.FieldCodes, string.Join(',', fieldCodes.Order(StringComparer.Ordinal))),
        ]);

    /// <summary>A revision's move along its state machine, with what the move set.</summary>
    public static AuditEntry ContributionTransition(
        string eventType, Guid actorId, ExternalContributionStatus from, ExternalUpdateRequest request, ExternalContribution contribution, Guid? nextContributionId = null) =>
        Entry(AuditEventClass.LifecycleTransition, eventType, actorId, request,
            new[]
            {
                AuditAttribute.Of(A.ExternalContributionId, contribution.Id),
                AuditAttribute.Of(A.RevisionNo, contribution.RevisionNo),
                AuditAttribute.Change(A.Status, from, contribution.Status),
                contribution.TargetVersion is null || from != ExternalContributionStatus.Draft ? null : AuditAttribute.Of(A.TargetVersion, contribution.TargetVersion),
                contribution.TargetState is null || from != ExternalContributionStatus.Draft ? null : AuditAttribute.Of(A.TargetState, contribution.TargetState),
                contribution.ReviewReason is null ? null : AuditAttribute.Of(A.Reason, AuditAttribute.Withheld),
                contribution.ReviewInternalNote is null ? null : AuditAttribute.Of(A.InternalNote, AuditAttribute.Withheld),
                nextContributionId is null ? null : AuditAttribute.Of(A.NextContributionId, nextContributionId),
            }.OfType<AuditAttribute>());

    /// <summary>
    /// An application attempt and its outcome; APPLIED and a terminal failure are lifecycle transitions of the revision, a conflict and a
    /// retryable refusal change nothing but the attempt's own record.
    /// </summary>
    public static AuditEntry Attempt(Guid actorId, ExternalUpdateRequest request, ExternalContribution contribution, SourceApplication application, ExternalContributionStatus from) =>
        Entry(
            contribution.Status == from ? AuditEventClass.DataChange : AuditEventClass.LifecycleTransition,
            application.Status switch
            {
                SourceApplicationStatus.Applied => ExternalParticipationAuditEvents.SourceApplied,
                SourceApplicationStatus.Conflict => ExternalParticipationAuditEvents.SourceApplicationConflict,
                SourceApplicationStatus.Failed => ExternalParticipationAuditEvents.SourceApplicationFailed,
                var status => throw new ArgumentOutOfRangeException(nameof(application), status, "Unknown application status."),
            },
            actorId,
            request,
            new[]
            {
                AuditAttribute.Of(A.ExternalContributionId, contribution.Id),
                AuditAttribute.Of(A.RevisionNo, contribution.RevisionNo),
                AuditAttribute.Of(A.SourceApplicationId, application.Id),
                AuditAttribute.Of(A.AttemptNo, application.AttemptNo),
                AuditAttribute.Of(A.TargetModule, request.TargetModule),
                AuditAttribute.Of(A.TargetId, request.TargetId),
                AuditAttribute.Of(A.ExpectedTargetVersion, application.ExpectedTargetRevisionNo),
                application.ActualTargetRevisionNo is null ? null : AuditAttribute.Of(A.ActualTargetVersion, application.ActualTargetRevisionNo),
                application.FailureCode is null ? null : AuditAttribute.Of(A.FailureCode, application.FailureCode),
                AuditAttribute.Change(A.Status, from, contribution.Status),
            }.OfType<AuditAttribute>());

    public static AuditEntry Revalidated(Guid actorId, ExternalUpdateRequest request, ExternalContribution contribution, SourceApplication application) =>
        Entry(AuditEventClass.DataChange, ExternalParticipationAuditEvents.SourceApplicationRevalidated, actorId, request,
        [
            AuditAttribute.Of(A.ExternalContributionId, contribution.Id),
            AuditAttribute.Of(A.SourceApplicationId, application.Id),
            AuditAttribute.Of(A.ActualTargetVersion, application.ActualTargetRevisionNo),
            AuditAttribute.Of(A.ExpectedTargetVersion, application.RevalidatedTargetRevisionNo),
        ]);

    /// <summary>ADR-013: an AHDA-only action refused to an external user, whatever they hold. The subject is the request, or null before one exists.</summary>
    public static AuditEntry AuthorityRefused(Guid actorId, Guid projectId, Guid externalEntityId, Guid? requestId, string permissionCode, string reason) =>
        new(AuditEventClass.AuthorizationDenial, ExternalParticipationAuditEvents.AuthorityRefused, AuditOutcome.Denied)
        {
            ActorUserId = actorId,
            Subject = requestId is { } id ? new AuditSubject(Module, RequestType, id) : null,
            ScopeProjectId = projectId,
            ScopeExternalEntityId = externalEntityId,
            Attributes = [AuditAttribute.Of(A.Permission, permissionCode), AuditAttribute.Of(A.Reason, reason)],
        };

    private static AuditAttribute[] RequestAttributes(ExternalUpdateRequest request) =>
    [
        AuditAttribute.Of(A.ExternalEntityId, request.ExternalEntityId),
        AuditAttribute.Of(A.ContributionTypeItemId, request.ContributionTypeItemId),
        AuditAttribute.Of(A.TargetModule, request.TargetModule),
        AuditAttribute.Of(A.TargetType, request.TargetType),
        AuditAttribute.Of(A.TargetId, request.TargetId),
        AuditAttribute.Of(A.ResponsibleUserId, request.ResponsibleUserId),
        AuditAttribute.Of(A.ReviewerUserId, request.ReviewerUserId),
        AuditAttribute.Of(A.DueDate, request.DueDate),
        AuditAttribute.Of(A.Status, request.Status),
    ];

    private static AuditEntry Entry(AuditEventClass eventClass, string eventType, Guid actorId, ExternalUpdateRequest request, IEnumerable<AuditAttribute> attributes) =>
        new(eventClass, eventType, AuditOutcome.Success)
        {
            ActorUserId = actorId,
            Subject = new AuditSubject(Module, RequestType, request.Id),
            ScopeProjectId = request.ProjectId,
            ScopeExternalEntityId = request.ExternalEntityId,
            Attributes = [.. attributes],
        };
}

/// <summary>A DRAFT request's own fields before a change, for its audit event.</summary>
internal sealed record RequestFields(Guid ContributionTypeItemId, Guid? TargetId, NarrativeText Instructions, Guid? ResponsibleUserId, Guid? ReviewerUserId, DateOnly? DueDate)
{
    public static RequestFields Of(ExternalUpdateRequest r) => new(r.ContributionTypeItemId, r.TargetId, r.Instructions, r.ResponsibleUserId, r.ReviewerUserId, r.DueDate);
}
