using PMPlatform.Application.Common.Auditing;
using PMPlatform.Application.Common.Authorization;
using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Application.Features.DocumentManagement.Contracts.Events;
using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

/// <summary>
/// Attachment, reference and evidence pinning for the modules that own business records (TASK-037). Unlinking ends a link
/// and withdraws its evidence; it never deletes a link, a document, a version or a file. Evidence is pinned to one CLEAN
/// version, so a later version of the document never changes what was evidenced.
/// </summary>
internal sealed class DocumentLinkService(
    IDocumentRepository repository, DocumentAccess access, DocumentReferences references, IAuditTrail audit, TimeProvider timeProvider) : IDocumentLinks
{
    public async Task<AdministrationResult<BusinessLinkDetail>> LinkAsync(
        Guid actorId, Guid documentId, BusinessTarget target, BusinessLinkRole role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();

        Document? document = await repository.FindDocumentAsync(documentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(actorId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (await repository.FindLinkAsync(documentId, target, role, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return await ExistingAsync(existing, cancellationToken).ConfigureAwait(false);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        BusinessLink link = new()
        {
            Id = Guid.CreateVersion7(now),
            DocumentId = documentId,
            LinkRole = role,
            TargetModule = target.Module,
            TargetType = target.Type,
            TargetId = target.Id,
            LinkedByUserId = actorId,
            LinkedAt = now,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        repository.Add(link);
        audit.Stage(DocumentAudit.Link(DocumentAuditEvents.DocumentLinked, actorId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), link));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            DocumentSaveOutcome.Saved => DocumentMapping.ToDetail(link, []),
            DocumentSaveOutcome.DuplicateLink => await ExistingAsync((await repository.FindLinkAsync(documentId, target, role, cancellationToken).ConfigureAwait(false))!, cancellationToken).ConfigureAwait(false),
            DocumentSaveOutcome.ConcurrencyConflict or DocumentSaveOutcome.DuplicateVersion or DocumentSaveOutcome.DuplicateEvidence =>
                throw new InvalidOperationException("Saving a new link met a conflict no link insert can cause."),
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<BusinessLinkDetail>> UnlinkAsync(Guid actorId, Guid businessLinkId, CancellationToken cancellationToken)
    {
        BusinessLink? link = await repository.FindLinkAsync(businessLinkId, cancellationToken).ConfigureAwait(false);
        Document? document = link is null ? null : await repository.FindDocumentAsync(link.DocumentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(actorId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!link!.IsActive)
        {
            return AdministrationError.TerminalState;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        DocumentAnchors anchors = await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false);
        link.UnlinkedAt = now;
        link.UnlinkedByUserId = actorId;
        Touch(link, actorId, now);
        audit.Stage(DocumentAudit.Link(DocumentAuditEvents.DocumentUnlinked, actorId, document, anchors, link));

        foreach (EvidenceReference evidence in (await repository.GetEvidenceAsync([link.Id], cancellationToken).ConfigureAwait(false)).Where(e => e.Status == EvidenceReferenceStatus.Valid))
        {
            evidence.Status = EvidenceReferenceStatus.Withdrawn;
            Touch(evidence, actorId, now);
            audit.Stage(DocumentAudit.Evidence(DocumentAuditEvents.EvidenceWithdrawn, actorId, document, anchors, link, evidence));
        }

        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) == DocumentSaveOutcome.Saved
            ? (await references.DetailsAsync([link], cancellationToken).ConfigureAwait(false))[0]
            : AdministrationError.TerminalState;
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> DesignateEvidenceAsync(Guid actorId, EvidenceDesignation designation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(designation);

        BusinessLink? link = await repository.FindLinkAsync(designation.BusinessLinkId, cancellationToken).ConfigureAwait(false);
        Document? document = link is null ? null : await repository.FindDocumentAsync(link.DocumentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(actorId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (!link!.IsActive)
        {
            return AdministrationError.Rule(DocumentErrorCodes.LinkEnded, new FieldIssue("businessLinkId", FieldIssue.Inactive));
        }

        IReadOnlyDictionary<Guid, DocumentVersion> versions = await repository.GetVersionsByIdAsync([designation.DocumentVersionId], cancellationToken).ConfigureAwait(false);
        if (!versions.TryGetValue(designation.DocumentVersionId, out DocumentVersion? version))
        {
            return AdministrationError.Rule(DocumentErrorCodes.ReferenceInvalid, new FieldIssue("documentVersionId", FieldIssue.NotFound));
        }

        if (version.DocumentId != link.DocumentId)
        {
            return AdministrationError.Rule(DocumentErrorCodes.VersionMismatch, new FieldIssue("documentVersionId", FieldIssue.NotAllowed));
        }

        // Acceptance criterion 1: a version SCAN_PENDING or QUARANTINED — or SCAN_FAILED — is never evidence.
        if (version.ScanState != ScanState.Clean)
        {
            return AdministrationError.Conflict(DocumentErrorCodes.NotAvailable);
        }

        if (await references.EvidenceTypeIssueAsync(designation.EvidenceTypeItemId, cancellationToken).ConfigureAwait(false) is { } issue)
        {
            return AdministrationError.Rule(DocumentErrorCodes.ReferenceInvalid, issue);
        }

        if (await repository.FindEvidenceAsync(link.Id, version.Id, designation.EvidenceTypeItemId, cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return ExistingEvidence(existing, link, version);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        EvidenceReference evidence = new()
        {
            Id = Guid.CreateVersion7(now),
            BusinessLinkId = link.Id,
            DocumentVersionId = version.Id,
            EvidenceTypeItemId = designation.EvidenceTypeItemId,
            DesignatedByUserId = actorId,
            DesignatedAt = now,
            Status = EvidenceReferenceStatus.Valid,
            CreatedAt = now,
            CreatedBy = actorId,
            UpdatedAt = now,
            UpdatedBy = actorId,
        };
        repository.Add(evidence);
        audit.Stage(DocumentAudit.Evidence(DocumentAuditEvents.EvidenceDesignated, actorId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), link, evidence));
        return await repository.SaveAsync(cancellationToken).ConfigureAwait(false) switch
        {
            DocumentSaveOutcome.Saved => DocumentMapping.ToDetail(evidence, link, version),
            DocumentSaveOutcome.DuplicateEvidence => ExistingEvidence(
                (await repository.FindEvidenceAsync(link.Id, version.Id, designation.EvidenceTypeItemId, cancellationToken).ConfigureAwait(false))!, link, version),
            DocumentSaveOutcome.ConcurrencyConflict or DocumentSaveOutcome.DuplicateVersion or DocumentSaveOutcome.DuplicateLink =>
                throw new InvalidOperationException("Saving new evidence met a conflict no evidence insert can cause."),
            _ => throw new InvalidOperationException("Unknown save outcome."),
        };
    }

    public async Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawEvidenceAsync(Guid actorId, Guid evidenceReferenceId, CancellationToken cancellationToken)
    {
        EvidenceReference? evidence = await repository.FindEvidenceAsync(evidenceReferenceId, cancellationToken).ConfigureAwait(false);
        BusinessLink? link = evidence is null ? null : await repository.FindLinkAsync(evidence.BusinessLinkId, cancellationToken).ConfigureAwait(false);
        Document? document = link is null ? null : await repository.FindDocumentAsync(link.DocumentId, null, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return AdministrationError.NotFound;
        }

        if (await access.CheckAsync(actorId, PermissionCatalogue.DocumentView, document, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (evidence!.Status != EvidenceReferenceStatus.Valid)
        {
            return AdministrationError.TerminalState;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        evidence.Status = EvidenceReferenceStatus.Withdrawn;
        Touch(evidence, actorId, now);
        audit.Stage(DocumentAudit.Evidence(DocumentAuditEvents.EvidenceWithdrawn, actorId, document, await access.AnchorsOfAsync(document, cancellationToken).ConfigureAwait(false), link!, evidence));
        if (await repository.SaveAsync(cancellationToken).ConfigureAwait(false) != DocumentSaveOutcome.Saved)
        {
            return AdministrationError.TerminalState;
        }

        IReadOnlyDictionary<Guid, DocumentVersion> versions = await repository.GetVersionsByIdAsync([evidence.DocumentVersionId], cancellationToken).ConfigureAwait(false);
        return DocumentMapping.ToDetail(evidence, link!, versions[evidence.DocumentVersionId]);
    }

    public async Task<IReadOnlyList<BusinessLinkDetail>> FindLinksAsync(BusinessTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        return await references.DetailsAsync(await repository.GetActiveLinksToAsync(target, cancellationToken).ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlySet<Guid>> GetSatisfiedEvidenceTypesAsync(BusinessTarget target, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Validate();
        return repository.GetSatisfiedEvidenceTypesAsync(target, cancellationToken);
    }

    /// <summary>An active link is the answer to linking again; an ended one stays ended (ERD unique key; F-7).</summary>
    private async Task<AdministrationResult<BusinessLinkDetail>> ExistingAsync(BusinessLink link, CancellationToken cancellationToken) =>
        link.IsActive
            ? (await references.DetailsAsync([link], cancellationToken).ConfigureAwait(false))[0]
            : AdministrationError.Rule(DocumentErrorCodes.LinkEnded, new FieldIssue("target", FieldIssue.Inactive));

    private static AdministrationResult<EvidenceReferenceDetail> ExistingEvidence(EvidenceReference evidence, BusinessLink link, DocumentVersion version) =>
        evidence.Status == EvidenceReferenceStatus.Valid
            ? DocumentMapping.ToDetail(evidence, link, version)
            : AdministrationError.Rule(DocumentErrorCodes.EvidenceWithdrawn, new FieldIssue("documentVersionId", FieldIssue.Inactive));

    private static void Touch(Domain.Common.AuditedEntity row, Guid by, DateTimeOffset now)
    {
        row.UpdatedAt = now;
        row.UpdatedBy = by;
    }
}
