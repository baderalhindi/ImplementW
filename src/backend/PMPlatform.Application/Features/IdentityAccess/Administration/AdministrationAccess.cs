using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Application.Features.IdentityAccess.Administration;

/// <summary>
/// The engine's record-level check for the administration services (authorization-engine.md §3 step 2). The endpoint gate
/// has already required the permission at some scope; this decides it on the record, 404 or 403 by R-47.
/// </summary>
internal sealed class AdministrationAccess(IAuthorizationEngine engine)
{
    /// <summary>
    /// A collection is served only to a caller whose grant covers a record with no anchor at all, which only ALL and
    /// READ-ONLY do. A narrower scope needs the collection filtered by scope, which does not exist yet (engine record
    /// F-8), so it is refused rather than shown everything.
    /// </summary>
    public async Task<AdministrationError?> CheckCollectionAsync(Guid actorId, string permissionCode, CancellationToken cancellationToken) =>
        (await DecideAsync(actorId, permissionCode, new AuthorizationSubject(), cancellationToken).ConfigureAwait(false)).IsAllowed
            ? null
            : AdministrationError.Forbidden;

    /// <summary>An existing record: 404 if the caller may not see it, 403 if they may see it but not do this.</summary>
    public async Task<AdministrationError?> CheckRecordAsync(Guid actorId, string permissionCode, AuthorizationSubject subject, CancellationToken cancellationToken) =>
        (await DecideAsync(actorId, permissionCode, subject, cancellationToken).ConfigureAwait(false)).Outcome switch
        {
            AuthorizationOutcome.Allowed => null,
            AuthorizationOutcome.NotFound => AdministrationError.NotFound,
            AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
            _ => AdministrationError.Forbidden,
        };

    /// <summary>A record about to be created, decided on the anchors it will have. Nothing exists to hide, so any refusal is 403.</summary>
    public async Task<AdministrationError?> CheckNewRecordAsync(Guid actorId, string permissionCode, AuthorizationSubject subject, CancellationToken cancellationToken) =>
        (await DecideAsync(actorId, permissionCode, subject, cancellationToken).ConfigureAwait(false)).IsAllowed
            ? null
            : AdministrationError.Forbidden;

    public static AuthorizationSubject SubjectOf(User user) => new()
    {
        DepartmentId = user.DepartmentId,
        ExternalEntityId = user.ExternalEntityId,
        OwnerUserId = user.Id,
    };

    public static AuthorizationSubject SubjectOf(UserDetail user) => new()
    {
        DepartmentId = user.DepartmentId,
        ExternalEntityId = user.ExternalEntityId,
        OwnerUserId = user.Id,
    };

    public static AuthorizationSubject SubjectOf(AccessRelationship assignment) => new()
    {
        DepartmentId = assignment.DepartmentId,
        ExternalEntityId = assignment.ExternalEntityId,
        ProjectId = assignment.ProjectId,
        OwnerUserId = assignment.UserId,
    };

    public static AuthorizationSubject SubjectOf(AccessRelationshipDetail assignment) => new()
    {
        DepartmentId = assignment.DepartmentId,
        ExternalEntityId = assignment.ExternalEntityId,
        ProjectId = assignment.ProjectId,
        OwnerUserId = assignment.UserId,
    };

    private Task<AuthorizationDecision> DecideAsync(Guid actorId, string permissionCode, AuthorizationSubject subject, CancellationToken cancellationToken) =>
        engine.AuthorizeAsync(actorId, new AuthorizationRequest(permissionCode, subject), cancellationToken);
}
