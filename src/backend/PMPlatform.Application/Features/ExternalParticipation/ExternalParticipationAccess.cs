using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.ExternalParticipation;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.ExternalParticipation;

/// <summary>
/// The explicit authorization check every WF-13 operation passes (M-7). A request is decided on its own anchors: its project, its project's
/// department and Project Manager as owner, its responder and reviewer as the people assigned to it, and — the isolation boundary — the
/// entity it is addressed to, not the project's delivering entity. So an external user reaches only requests of their own entity, on the
/// projects their assignment covers (ADR-013: cross-entity isolation is absolute), whatever project they are on. Refusals are audited by the
/// engine; an external user refused an AHDA-only action is audited here.
/// </summary>
internal sealed class ExternalParticipationAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    public const string ExternalUser = "EXTERNAL_USER";

    /// <summary>
    /// A request's subject. <paramref name="workflowActors"/>, when given, are the only people who may act: the responder for an answer, the
    /// reviewer for a review — an empty set when nobody is named, so nobody may.
    /// </summary>
    public static AuthorizationSubject SubjectOf(
        ProjectFacts project, Guid externalEntityId, Guid? responsibleUserId, Guid? reviewerUserId, IReadOnlyCollection<Guid>? workflowActors = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new AuthorizationSubject
        {
            ProjectId = project.Id,
            DepartmentId = project.DepartmentId,
            ExternalEntityId = externalEntityId,
            OwnerUserId = project.ProjectManagerUserId,
            AssignedUserIds = [.. new[] { responsibleUserId, reviewerUserId }.OfType<Guid>()],
            WorkflowActorUserIds = workflowActors,
        };
    }

    public static AuthorizationSubject SubjectOf(ProjectFacts project, ExternalUpdateRequest request, IReadOnlyCollection<Guid>? workflowActors = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SubjectOf(project, request.ExternalEntityId, request.ResponsibleUserId, request.ReviewerUserId, workflowActors);
    }

    /// <summary>
    /// Null when allowed; otherwise NotFound (R-47) or Forbidden, and for a write to a CLOSED project 409 PROJECT_CLOSED (TASK-063): a closed
    /// project's history is read-only (WF-13 BR-EXT-039).
    /// </summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectFacts project, AuthorizationSubject subject, CancellationToken cancellationToken) =>
        (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, subject), cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => ClosedProjectGuard.Refusal(project, permissionCode),
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            var outcome => throw new ArgumentOutOfRangeException(nameof(permissionCode), outcome, "Unknown authorization outcome."),
        };

    /// <summary>
    /// As <see cref="CheckAsync"/>, for AHDA's side — requesting, reviewing, applying: an external user is refused whatever they hold, since R04
    /// is held by internal and external users alike, and the refusal is audited (TASK-066 gate decision; ADR-013).
    /// </summary>
    public async Task<AdministrationError?> CheckInternalAsync(
        Guid callerId, string permissionCode, ProjectFacts project, AuthorizationSubject subject, Func<string, AuditEntry> refusal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        if (await CheckAsync(callerId, permissionCode, project, subject, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await IsInternalAsync(callerId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        await audit.RecordAsync(refusal(ExternalUser)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    /// <summary>Whether the caller may see the request, for a collection: nothing is recorded (R-3).</summary>
    public async Task<bool> CanViewAsync(Guid callerId, AuthorizationSubject subject, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(PermissionCatalogue.ExternalRequestView, subject), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>The requests the caller's EXTERNAL_REQUEST_VIEW reaches, as clauses for the query.</summary>
    public Task<RecordScope> ViewScopeAsync(Guid callerId, CancellationToken cancellationToken) =>
        engine.GetRecordScopeAsync(callerId, PermissionCatalogue.ExternalRequestView, cancellationToken);

    /// <summary>AHDA's people get the internal view; anyone else — an external user, or a caller the engine does not know — the external projection.</summary>
    public async Task<bool> IsInternalAsync(Guid userId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) is { IsActive: true, UserType: UserType.Internal };

    /// <summary>
    /// WF-13 §5.3, EXT-CC-03: a responder is an active external user of the request's entity whose own grants let them answer this request
    /// once named on it — so their assignment covers its project (the explicit project grant) and nothing is extended to make them eligible.
    /// </summary>
    public async Task<bool> IsEligibleResponderAsync(Guid userId, ProjectFacts project, Guid externalEntityId, CancellationToken cancellationToken) =>
        await engine.GetPrincipalAsync(userId, cancellationToken).ConfigureAwait(false) is { IsActive: true, UserType: UserType.External } principal
        && principal.ExternalEntityId == externalEntityId
        && (await engine.EvaluateAsync(
                userId,
                new AuthorizationRequest(PermissionCatalogue.ExternalContributionRespond, SubjectOf(project, externalEntityId, userId, null, [userId])),
                cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>A reviewer is an active internal user whose own grants let them review this request once named on it (EXT-CC-10).</summary>
    public async Task<bool> IsEligibleReviewerAsync(Guid userId, ProjectFacts project, Guid externalEntityId, CancellationToken cancellationToken) =>
        await IsInternalAsync(userId, cancellationToken).ConfigureAwait(false)
        && (await engine.EvaluateAsync(
                userId,
                new AuthorizationRequest(PermissionCatalogue.ExternalContributionReview, SubjectOf(project, externalEntityId, null, userId, [userId])),
                cancellationToken).ConfigureAwait(false)).IsAllowed;
}
