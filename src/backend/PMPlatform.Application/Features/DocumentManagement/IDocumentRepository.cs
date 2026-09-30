using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>The <c>document_management</c> schema (TASK-037). Finds that return rows to change track them; the others do not.</summary>
public interface IDocumentRepository
{
    /// <summary>
    /// The project's scope anchors, or null if there is no such project. Read from <c>project.project</c> until the Project
    /// module publishes a contract for them (document-management.md F-6).
    /// </summary>
    public Task<DocumentAnchors?> FindProjectAnchorsAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>Tracked. With <paramref name="expectedVersion"/>, the next save is conditional on it (R-21).</summary>
    public Task<Document?> FindDocumentAsync(Guid documentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>The row version of a tracked document, as last read or saved: its ETag.</summary>
    public uint RowVersionOf(Document document);

    /// <summary>
    /// The documents <paramref name="scope"/> reaches that match the query, most recently changed first, one page, with the
    /// total count. Not tracked.
    /// </summary>
    public Task<(IReadOnlyList<Document> Items, int TotalCount)> ListAsync(RecordScope scope, DocumentQuery query, CancellationToken cancellationToken);

    /// <summary>Each document's highest-numbered version. Not tracked.</summary>
    public Task<IReadOnlyDictionary<Guid, DocumentVersion>> GetLatestVersionsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken cancellationToken);

    /// <summary>Every version of the document, newest first. Not tracked.</summary>
    public Task<IReadOnlyList<DocumentVersion>> GetVersionsAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Not tracked.</summary>
    public Task<IReadOnlyDictionary<Guid, DocumentVersion>> GetVersionsByIdAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<DocumentVersion?> FindVersionAsync(Guid versionId, CancellationToken cancellationToken);

    /// <summary>The document's highest version number; 0 before its first.</summary>
    public Task<int> GetLatestVersionNoAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>SCAN_PENDING versions, oldest upload first.</summary>
    public Task<IReadOnlyList<Guid>> FindPendingScanIdsAsync(int count, CancellationToken cancellationToken);

    /// <summary>Every link of the document, ended ones included, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<BusinessLink>> GetLinksAsync(Guid documentId, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<BusinessLink?> FindLinkAsync(Guid businessLinkId, CancellationToken cancellationToken);

    /// <summary>The link of this document, target and role, ended or not (ERD unique key). Not tracked.</summary>
    public Task<BusinessLink?> FindLinkAsync(Guid documentId, BusinessTarget target, BusinessLinkRole role, CancellationToken cancellationToken);

    /// <summary>The target's links not ended, oldest first. Not tracked.</summary>
    public Task<IReadOnlyList<BusinessLink>> GetActiveLinksToAsync(BusinessTarget target, CancellationToken cancellationToken);

    /// <summary>The evidence designated on the links, oldest first. Tracked.</summary>
    public Task<IReadOnlyList<EvidenceReference>> GetEvidenceAsync(IReadOnlyCollection<Guid> businessLinkIds, CancellationToken cancellationToken);

    /// <summary>Tracked.</summary>
    public Task<EvidenceReference?> FindEvidenceAsync(Guid evidenceReferenceId, CancellationToken cancellationToken);

    /// <summary>The reference for this link, version and type, VALID or not (ERD unique key). Not tracked.</summary>
    public Task<EvidenceReference?> FindEvidenceAsync(Guid businessLinkId, Guid documentVersionId, Guid evidenceTypeItemId, CancellationToken cancellationToken);

    /// <summary>Evidence types with a VALID reference, on a link to the target not ended, pinned to a CLEAN version.</summary>
    public Task<IReadOnlySet<Guid>> GetSatisfiedEvidenceTypesAsync(BusinessTarget target, CancellationToken cancellationToken);

    public void Add(Document document);

    public void Add(DocumentVersion version);

    public void Add(BusinessLink link);

    public void Add(EvidenceReference evidence);

    /// <summary>
    /// Saves the tracked changes. A row changed since it was read, and a unique key another request took first, are answers,
    /// not faults; after either nothing stays tracked.
    /// </summary>
    public Task<DocumentSaveOutcome> SaveAsync(CancellationToken cancellationToken);
}
