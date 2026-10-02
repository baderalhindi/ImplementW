using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Application.Features.Project.Contracts;
using PMPlatform.Domain.IdentityAccess;
using PMPlatform.Domain.Progress;

namespace PMPlatform.Application.Features.Progress;

/// <summary>
/// The explicit authorization check every progress operation passes (M-7), decided on the project's anchors: the project,
/// its owning department, its delivering entity, and its Project Manager as owner. Refusals are audited by the engine.
/// </summary>
internal sealed class ProgressAccess(IAuthorizationEngine engine, IAuditTrail audit)
{
    public const string ExternalUser = "EXTERNAL_USER";
    public const string Submitter = "SUBMITTER";

    /// <summary>Null when allowed; otherwise NotFound (R-47) or Forbidden.</summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => null,
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            var outcome => throw new ArgumentOutOfRangeException(nameof(permissionCode), outcome, "Unknown authorization outcome."),
        };

    /// <summary>
    /// Whether the caller may see the project's progress, for a collection: nothing is recorded, because a collection that
    /// comes back empty is not a refused request (api-conventions R-3).
    /// </summary>
    public async Task<bool> CanViewAsync(Guid callerId, ProjectFacts project, CancellationToken cancellationToken) =>
        (await engine.EvaluateAsync(callerId, new AuthorizationRequest(PermissionCatalogue.ProgressView, SubjectOf(project)), cancellationToken).ConfigureAwait(false)).IsAllowed;

    /// <summary>
    /// Review is AHDA's gate over published progress (ADR-013: "published progress remains governed"): an external user is
    /// refused whatever they hold, and so is the person who submitted the revision — no one publishes their own progress.
    /// Both refusals are audited here.
    /// </summary>
    public async Task<AdministrationError?> CheckReviewAsync(Guid callerId, ProjectFacts project, ProgressSubmission submission, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(submission);

        if (await CheckAsync(callerId, PermissionCatalogue.ProgressReview, project, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        string? reason = await engine.GetPrincipalAsync(callerId, cancellationToken).ConfigureAwait(false) is not { UserType: UserType.Internal } ? ExternalUser
            : submission.SubmittedByUserId == callerId ? Submitter
            : null;
        if (reason is null)
        {
            return null;
        }

        await audit.RecordAsync(ProgressAudit.ReviewRefused(callerId, project, submission, reason)).ConfigureAwait(false);
        return AdministrationError.Forbidden;
    }

    private static AuthorizationSubject SubjectOf(ProjectFacts project)
    {
        ArgumentNullException.ThrowIfNull(project);
        return new AuthorizationSubject
        {
            ProjectId = project.Id,
            DepartmentId = project.DepartmentId,
            ExternalEntityId = project.ExternalEntityId,
            OwnerUserId = project.ProjectManagerUserId,
        };
    }
}
