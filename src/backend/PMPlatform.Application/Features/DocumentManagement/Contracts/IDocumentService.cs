using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// WF-12 as the API serves it (SCR-120–124, MOD-050–053). Every operation on a document is decided by the authorization
/// engine on that document's anchors — its project, the project's department and entity, its owner and its
/// classification — never by project membership alone (CTL-20). A document the caller may not read is 404 (R-47).
/// </summary>
public interface IDocumentService
{
    public Task<DocumentPage> ListAsync(Guid callerId, DocumentQuery query, CancellationToken cancellationToken);

    public Task<AdministrationResult<Versioned<DocumentDetail>>> GetAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken);

    /// <summary>MOD-050: the document and its version 1, SCAN_PENDING until scanned.</summary>
    public Task<AdministrationResult<Versioned<DocumentDetail>>> CreateAsync(Guid callerId, DocumentDraft draft, DocumentFile file, CancellationToken cancellationToken);

    /// <summary>MOD-051. Requires the caller's version of the document (R-21).</summary>
    public Task<AdministrationResult<Versioned<DocumentDetail>>> UpdateAsync(
        Guid callerId, Guid documentId, DocumentChanges changes, uint expectedVersion, CancellationToken cancellationToken);

    /// <summary>MOD-053: ACTIVE → ARCHIVED. Its versions, links and evidence remain.</summary>
    public Task<AdministrationResult<Versioned<DocumentDetail>>> ArchiveAsync(Guid callerId, Guid documentId, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>SCR-124: every version, newest first.</summary>
    public Task<AdministrationResult<IReadOnlyList<DocumentVersionDetail>>> ListVersionsAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken);

    public Task<AdministrationResult<DocumentVersionDetail>> GetVersionAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken);

    /// <summary>MOD-052 Replace Version: a new version; the earlier ones, and evidence pinned to them, are unchanged.</summary>
    public Task<AdministrationResult<DocumentVersionDetail>> AddVersionAsync(Guid callerId, Guid documentId, DocumentFile file, CancellationToken cancellationToken);

    /// <summary>SCAN_FAILED → SCAN_PENDING, for the scanner to try again.</summary>
    public Task<AdministrationResult<DocumentVersionDetail>> RescanAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken);

    /// <summary>The content of a CLEAN version; any other state is 409 <see cref="DocumentErrorCodes.NotAvailable"/>.</summary>
    public Task<AdministrationResult<DocumentContent>> OpenContentAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken);

    /// <summary>Every link of the document, ended ones included, with their evidence: the document's full business history.</summary>
    public Task<AdministrationResult<IReadOnlyList<BusinessLinkDetail>>> ListLinksAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken);

    public Task<AdministrationResult<EvidenceReferenceDetail>> GetEvidenceAsync(Guid callerId, Guid evidenceReferenceId, CancellationToken cancellationToken);

    /// <summary>The evidence path: the pinned version's content, only if it is CLEAN (R-8).</summary>
    public Task<AdministrationResult<DocumentContent>> OpenEvidenceContentAsync(Guid callerId, Guid evidenceReferenceId, CancellationToken cancellationToken);
}
