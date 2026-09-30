using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// The explicit authorization check every document operation passes (CTL-20): the engine decides on the document's own
/// anchors — project, the project's department and entity, owner, classification — so being on the project is never
/// enough by itself. Refusals are audited by the engine.
/// </summary>
internal sealed class DocumentAccess(IAuthorizationEngine engine, IDocumentRepository repository)
{
    private readonly Dictionary<Guid, DocumentAnchors> _projects = [];

    /// <summary>Null when allowed; otherwise NotFound (R-47) or Forbidden.</summary>
    public async Task<AdministrationError?> CheckAsync(Guid callerId, string permissionCode, Document document, CancellationToken cancellationToken)
    {
        AuthorizationSubject subject = await SubjectOfAsync(document.ProjectId, document.OwnerUserId, document.DataClassificationItemId, cancellationToken).ConfigureAwait(false);
        return Refusal(await engine.AuthorizeAsync(callerId, new AuthorizationRequest(permissionCode, subject), cancellationToken).ConfigureAwait(false));
    }

    public Task<DocumentAnchors> AnchorsOfAsync(Document document, CancellationToken cancellationToken) =>
        AnchorsOfAsync(document.ProjectId, cancellationToken);

    public async Task<AuthorizationSubject> SubjectOfAsync(Guid? projectId, Guid ownerUserId, Guid classificationItemId, CancellationToken cancellationToken)
    {
        DocumentAnchors anchors = await AnchorsOfAsync(projectId, cancellationToken).ConfigureAwait(false);
        return new AuthorizationSubject
        {
            ProjectId = anchors.ProjectId,
            DepartmentId = anchors.DepartmentId,
            ExternalEntityId = anchors.ExternalEntityId,
            OwnerUserId = ownerUserId,
            DataClassificationItemId = classificationItemId,
        };
    }

    public static AdministrationError? Refusal(AuthorizationDecision decision) => decision.Outcome switch
    {
        AuthorizationOutcome.Allowed => null,
        AuthorizationOutcome.NotFound => AdministrationError.NotFound,
        AuthorizationOutcome.Forbidden => AdministrationError.Forbidden,
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Outcome, "Unknown authorization outcome."),
    };

    private async Task<DocumentAnchors> AnchorsOfAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (projectId is not { } project)
        {
            return DocumentAnchors.Library;
        }

        if (!_projects.TryGetValue(project, out DocumentAnchors? anchors))
        {
            // A document's project is a foreign key, so it exists; the fallback keeps the per-project bound even so.
            anchors = await repository.FindProjectAnchorsAsync(project, cancellationToken).ConfigureAwait(false) ?? new DocumentAnchors(project, null, null);
            _projects[project] = anchors;
        }

        return anchors;
    }
}
