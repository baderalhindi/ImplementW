using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.DocumentManagement.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// WF-12 upload, versioning, scan status and download (TASK-037). A version's bytes never change once stored; a new file
/// is a new version. Content is served only from a CLEAN version, on the document path and the evidence path alike.
/// </summary>
internal sealed class DocumentService(
    IDocumentRepository repository,
    DocumentAccess access,
    DocumentReferences references,
    DocumentUploads uploads,
    IDocumentStorage storage,
    IAuthorizationEngine engine,
    IAuditTrail audit,
    TimeProvider timeProvider) : IDocumentService
{
    public async Task<DocumentPage> ListAsync(Guid callerId, DocumentQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        RecordScope scope = await engine.GetRecordScopeAsync(callerId, PermissionCatalogue.DocumentView, cancellationToken).ConfigureAwait(false);
        if (scope.IsEmpty)
        {
            return new DocumentPage([], query.Page.Page, query.Page.PageSize, 0);
        }

        (IReadOnlyList<Document> items, int total) = await repository.ListAsync(scope, query, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, DocumentVersion> latest = await repository.GetLatestVersionsAsync([.. items.Select(d => d.Id)], cancellationToken).ConfigureAwait(false);
        return new DocumentPage([.. items.Select(d => DocumentMapping.ToSummary(d, latest.GetValueOrDefault(d.Id)))], query.Page.Page, query.Page.PageSize, total);
    }

    public async Task<AdministrationResult<Versioned<DocumentDetail>>> GetAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken)
    {
        Document? document = await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false);
        return document is null ? AdministrationError.NotFound
            : await access.CheckAsync(callerId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused ? refused
            : await VersionedDetailAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DocumentDetail>>> CreateAsync(Guid callerId, DocumentDraft draft, DocumentFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        if (uploads.Check(file) is { } unacceptable)
        {
            return unacceptable;
        }

        if (await references.MetadataIssueAsync(draft.DocumentTypeItemId, draft.DataClassificationItemId, cancellationToken).ConfigureAwait(false) is { } issue)
        {
            return AdministrationError.Rule(DocumentErrorCodes.ReferenceInvalid, issue);
        }

        if (draft.ProjectId is { } projectId && await repository.FindProjectAnchorsAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return AdministrationError.Rule(DocumentErrorCodes.ReferenceInvalid, new FieldIssue("projectId", FieldIssue.NotFound));
        }

        // The uploader becomes the owner, and must be allowed to upload a document of this classification to this project.
        // A document that does not exist yet has nothing to hide, so a refusal is 403 whatever the engine's R-47 answer.
        AuthorizationSubject subject = await access.SubjectOfAsync(draft.ProjectId, callerId, draft.DataClassificationItemId, cancellationToken).ConfigureAwait(false);
        if (!(await engine.AuthorizeAsync(callerId, new AuthorizationRequest(PermissionCatalogue.DocumentUpload, subject), cancellationToken).ConfigureAwait(false)).IsAllowed)
        {
            return AdministrationError.Forbidden;
        }

        if (await access.ClosedRefusalAsync(PermissionCatalogue.DocumentUpload, draft.ProjectId, cancellationToken).ConfigureAwait(false) is { } closed)
        {
            return closed;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Document document = new()
        {
            Id = Guid.CreateVersion7(now),
            Title = draft.Title,
            Description = draft.Description,
            DocumentTypeItemId = draft.DocumentTypeItemId,
            DataClassificationItemId = draft.DataClassificationItemId,
            ProjectId = draft.ProjectId,
            OwnerUserId = callerId,
            Status = DocumentStatus.Active,
            CreatedAt = now,
            CreatedBy = callerId,
            UpdatedAt = now,
            UpdatedBy = callerId,
        };
        DocumentVersion version = await uploads.StoreAsync(document.Id, 1, file, callerId, now, cancellationToken).ConfigureAwait(false);
        repository.Add(document);
        repository.Add(version);
        audit.Stage(DocumentAudit.Uploaded(callerId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), version));
        await SaveNewAsync(cancellationToken).ConfigureAwait(false);
        return new Versioned<DocumentDetail>(DocumentMapping.ToDetail(document, version), repository.RowVersionOf(document));
    }

    public async Task<AdministrationResult<Versioned<DocumentDetail>>> UpdateAsync(
        Guid callerId, Guid documentId, DocumentChanges changes, uint expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        Document? document = await repository.FindDocumentAsync(documentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentManage, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (document.Status != DocumentStatus.Active)
        {
            return AdministrationError.TerminalState;
        }

        if (await references.MetadataIssueAsync(changes.DocumentTypeItemId, changes.DataClassificationItemId, cancellationToken).ConfigureAwait(false) is { } issue)
        {
            return AdministrationError.Rule(DocumentErrorCodes.ReferenceInvalid, issue);
        }

        // Reclassifying needs clearance for the new classification as well as the old: no one moves a document to a level
        // they could not then manage (ADR-010).
        if (changes.DataClassificationItemId != document.DataClassificationItemId)
        {
            AuthorizationSubject reclassified = await access.SubjectOfAsync(document.ProjectId, document.OwnerUserId, changes.DataClassificationItemId, cancellationToken).ConfigureAwait(false);
            if (!(await engine.AuthorizeAsync(callerId, new AuthorizationRequest(PermissionCatalogue.DocumentManage, reclassified), cancellationToken).ConfigureAwait(false)).IsAllowed)
            {
                return AdministrationError.Forbidden;
            }
        }

        AuditAttribute[] changed =
        [
            .. new[]
            {
                AuditAttribute.WithheldChange(DocumentAuditAttributes.TitleText, document.Title.Text, changes.Title.Text),
                AuditAttribute.WithheldChange(DocumentAuditAttributes.DescriptionText, document.Description?.Text, changes.Description?.Text),
                AuditAttribute.Change(DocumentAuditAttributes.DocumentTypeItemId, document.DocumentTypeItemId, changes.DocumentTypeItemId),
                AuditAttribute.Change(DocumentAuditAttributes.DataClassificationItemId, document.DataClassificationItemId, changes.DataClassificationItemId),
            }.OfType<AuditAttribute>(),
        ];
        DocumentAnchors anchors = await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false);
        document.Title = changes.Title;
        document.Description = changes.Description;
        document.DocumentTypeItemId = changes.DocumentTypeItemId;
        document.DataClassificationItemId = changes.DataClassificationItemId;
        Touch(document, callerId);
        audit.Stage(DocumentAudit.MetadataChanged(callerId, document, anchors, changed));
        return await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<Versioned<DocumentDetail>>> ArchiveAsync(Guid callerId, Guid documentId, uint? expectedVersion, CancellationToken cancellationToken)
    {
        Document? document = await repository.FindDocumentAsync(documentId, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentManage, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (document.Status != DocumentStatus.Active)
        {
            return AdministrationError.TerminalState;
        }

        document.Status = DocumentStatus.Archived;
        Touch(document, callerId);
        audit.Stage(DocumentAudit.Archived(callerId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false)));
        return await SaveDocumentAsync(document, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<IReadOnlyList<DocumentVersionDetail>>> ListVersionsAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken)
    {
        Document? document = await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        IReadOnlyList<DocumentVersion> versions = await repository.GetVersionsAsync(documentId, cancellationToken).ConfigureAwait(false);
        return new List<DocumentVersionDetail>(versions.Select(DocumentMapping.ToDetail));
    }

    public async Task<AdministrationResult<DocumentVersionDetail>> GetVersionAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken)
    {
        (DocumentVersion? version, AdministrationError? refused) = await FindReadableVersionAsync(callerId, documentId, versionId, PermissionCatalogue.DocumentView, cancellationToken).ConfigureAwait(false);
        return version is null ? refused! : DocumentMapping.ToDetail(version);
    }

    public async Task<AdministrationResult<DocumentVersionDetail>> AddVersionAsync(Guid callerId, Guid documentId, DocumentFile file, CancellationToken cancellationToken)
    {
        Document? document = await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentUpload, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (document.Status != DocumentStatus.Active)
        {
            return AdministrationError.TerminalState;
        }

        if (uploads.Check(file) is { } unacceptable)
        {
            return unacceptable;
        }

        // The document's own row changes too, so two versions added at once cannot both commit: its row version guards
        // the number, and the unique (document, number) key backs it.
        DateTimeOffset now = timeProvider.GetUtcNow();
        int versionNo = await repository.GetLatestVersionNoAsync(documentId, cancellationToken).ConfigureAwait(false) + 1;
        DocumentVersion version = await uploads.StoreAsync(documentId, versionNo, file, callerId, now, cancellationToken).ConfigureAwait(false);
        repository.Add(version);
        Touch(document, callerId);
        audit.Stage(DocumentAudit.VersionAdded(callerId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), version));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DocumentSaveOutcome.Saved
            ? DocumentMapping.ToDetail(version)
            : AdministrationError.Conflict(DocumentErrorCodes.VersionConflict);
    }

    public async Task<AdministrationResult<DocumentVersionDetail>> RescanAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken)
    {
        (DocumentVersion? version, AdministrationError? refused) = await FindReadableVersionAsync(callerId, documentId, versionId, PermissionCatalogue.DocumentManage, cancellationToken).ConfigureAwait(false);
        if (version is null)
        {
            return refused!;
        }

        if (version.ScanState != ScanState.ScanFailed)
        {
            return AdministrationError.InvalidTransition;
        }

        Document document = (await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false))!;
        version.ScanState = ScanState.ScanPending;
        version.ScanCompletedAt = null;
        version.ScanReference = null;
        Touch(version, callerId);
        audit.Stage(DocumentAudit.ScanRequeued(callerId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), version));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DocumentSaveOutcome.Saved
            ? DocumentMapping.ToDetail(version)
            : AdministrationError.Conflict(DocumentErrorCodes.VersionConflict);
    }

    public async Task<AdministrationResult<DocumentContent>> OpenContentAsync(Guid callerId, Guid documentId, Guid versionId, CancellationToken cancellationToken)
    {
        (DocumentVersion? version, AdministrationError? refused) = await FindReadableVersionAsync(callerId, documentId, versionId, PermissionCatalogue.DocumentView, cancellationToken).ConfigureAwait(false);
        return version is null ? refused! : await OpenCleanAsync(callerId, version, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AdministrationResult<IReadOnlyList<BusinessLinkDetail>>> ListLinksAsync(Guid callerId, Guid documentId, CancellationToken cancellationToken)
    {
        Document? document = await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        IReadOnlyList<BusinessLink> links = await repository.GetLinksAsync(documentId, cancellationToken).ConfigureAwait(false);
        return new List<BusinessLinkDetail>(await references.DetailsAsync(links, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> GetEvidenceAsync(Guid callerId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        (EvidenceReference? evidence, BusinessLink? link, DocumentVersion? version, AdministrationError? refused) =
            await FindReadableEvidenceAsync(callerId, evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        return evidence is null ? refused! : DocumentMapping.ToDetail(evidence, link!, version!);
    }

    public async Task<AdministrationResult<DocumentContent>> OpenEvidenceContentAsync(Guid callerId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        (EvidenceReference? evidence, _, DocumentVersion? version, AdministrationError? refused) =
            await FindReadableEvidenceAsync(callerId, evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        return evidence is null ? refused! : await OpenCleanAsync(callerId, version!, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The version, if it is the document's and the caller holds <paramref name="permissionCode"/> on the document.</summary>
    private async Task<(DocumentVersion? Version, AdministrationError? Refused)> FindReadableVersionAsync(
        Guid callerId, Guid documentId, Guid versionId, string permissionCode, CancellationToken cancellationToken)
    {
        DocumentVersion? version = await repository.FindVersionAsync(versionId, cancellationToken).ConfigureAwait(false);
        Document? document = version?.DocumentId == documentId
            ? await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false)
            : null;
        if (document is null)
        {
            return (null, AdministrationError.NotFound);
        }

        AdministrationError? refused = await access.CheckAsync(callerId, permissionCode, document, cancellationToken).ConfigureAwait(false);
        return refused is null ? (version, null) : (null, refused);
    }

    /// <summary>The evidence with its link and pinned version, if the caller may read the link's document.</summary>
    private async Task<(EvidenceReference? Evidence, BusinessLink? Link, DocumentVersion? Version, AdministrationError? Refused)> FindReadableEvidenceAsync(
        Guid callerId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        EvidenceReference? evidence = await repository.FindEvidenceAsync(evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        BusinessLink? link = evidence is null ? null : await repository.FindLinkAsync(evidence.BusinessLinkId, cancellationToken).ConfigureAwait(false);
        Document? document = link is null ? null : await repository.FindDocumentAsync(link.DocumentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return (null, null, null, AdministrationError.NotFound);
        }

        if (await access.CheckAsync(callerId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return (null, null, null, refused);
        }

        IReadOnlyDictionary<Guid, DocumentVersion> versions = await repository.GetVersionsByIdAsync([evidence!.DocumentVersionId], cancellationToken).ConfigureAwait(false);
        return (evidence, link, versions[evidence.DocumentVersionId], null);
    }

    /// <summary>
    /// Only a CLEAN version's bytes leave the store (CTL-20): SCAN_PENDING, SCAN_FAILED and QUARANTINED are 409
    /// DOCUMENT_NOT_AVAILABLE, and an attempt on QUARANTINED content is audited.
    /// </summary>
    private async Task<AdministrationResult<DocumentContent>> OpenCleanAsync(Guid callerId, DocumentVersion version, CancellationToken cancellationToken)
    {
        if (version.ScanState != ScanState.Clean)
        {
            if (version.ScanState == ScanState.Quarantined)
            {
                Document document = (await repository.FindDocumentAsync(version.DocumentId, null, cancellationToken).ConfigureAwait(false))!;
                DocumentAnchors anchors = await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false);
                await audit.RecordAsync(DocumentAudit.QuarantinedContentWithheld(callerId, document, anchors, version)).ConfigureAwait(false);
            }

            return AdministrationError.Conflict(DocumentErrorCodes.NotAvailable);
        }

        if (!storage.IsConfigured)
        {
            return AdministrationError.Unavailable;
        }

        Stream content = await storage.OpenReadAsync(version.StorageObjectKey, cancellationToken).ConfigureAwait(false);
        return new DocumentContent(version.FileName, version.SizeBytes, version.ChecksumSha256, content);
    }

    private async Task<Versioned<DocumentDetail>> VersionedDetailAsync(Document document, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, DocumentVersion> latest = await repository.GetLatestVersionsAsync([document.Id], cancellationToken).ConfigureAwait(false);
        return new Versioned<DocumentDetail>(DocumentMapping.ToDetail(document, latest.GetValueOrDefault(document.Id)), repository.RowVersionOf(document));
    }

    private async Task<AdministrationResult<Versioned<DocumentDetail>>> SaveDocumentAsync(Document document, CancellationToken cancellationToken) =>
        await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DocumentSaveOutcome.Saved
            ? await VersionedDetailAsync(document, cancellationToken).ConfigureAwait(false)
            : AdministrationError.PreconditionFailed;

    /// <summary>New rows with new ids: no conflict is expected, so any is a fault.</summary>
    private async Task SaveNewAsync(CancellationToken cancellationToken)
    {
        DocumentSaveOutcome outcome = await repository.SaveAsync(cancellationToken).ConfigureAwait(false);
        if (outcome != DocumentSaveOutcome.Saved)
        {
            throw new InvalidOperationException($"Saving a new document failed: {outcome}.");
        }
    }

    private void Touch(Domain.Common.AuditedEntity row, Guid by)
    {
        row.UpdatedAt = timeProvider.GetUtcNow();
        row.UpdatedBy = by;
    }
}
