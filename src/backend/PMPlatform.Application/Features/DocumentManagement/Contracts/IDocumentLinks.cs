using PMPlatform.Application.Features.IdentityAccess.Contracts.Administration;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// ADR-003 §8.2 edges 16 and 17: how a module (Milestone, ExternalParticipation) attaches documents to its records and
/// pins evidence. The calling module authorizes its own record; DocumentManagement authorizes the document — the actor
/// must be able to read it. Each command saves the request's unit of work, so the caller's staged changes commit with it.
/// </summary>
public interface IDocumentLinks
{
    /// <summary>An active link of the same document, target and role is returned as it is.</summary>
    public Task<AdministrationResult<BusinessLinkDetail>> LinkAsync(
        Guid actorId, Guid documentId, BusinessTarget target, BusinessLinkRole role, CancellationToken cancellationToken);

    /// <summary>Ends the link and withdraws its VALID evidence. Nothing is deleted: the document, its versions and the link remain.</summary>
    public Task<AdministrationResult<BusinessLinkDetail>> UnlinkAsync(Guid actorId, Guid businessLinkId, CancellationToken cancellationToken);

    /// <summary>
    /// Pins the version as evidence. Only a CLEAN version can be pinned (409 <see cref="DocumentErrorCodes.NotAvailable"/>),
    /// on an active link, of the link's own document. A VALID reference for the same link, version and type is returned.
    /// </summary>
    public Task<AdministrationResult<EvidenceReferenceDetail>> DesignateEvidenceAsync(Guid actorId, EvidenceDesignation designation, CancellationToken cancellationToken);

    public Task<AdministrationResult<EvidenceReferenceDetail>> WithdrawEvidenceAsync(Guid actorId, Guid evidenceReferenceId, CancellationToken cancellationToken);

    /// <summary>The target's active links and their evidence. Unauthorized: the caller has authorized its own record.</summary>
    public Task<IReadOnlyList<BusinessLinkDetail>> FindLinksAsync(BusinessTarget target, CancellationToken cancellationToken);

    /// <summary>
    /// The evidence types the target holds satisfying evidence for: a VALID reference, on an active link, pinned to a CLEAN
    /// version. A version SCAN_PENDING, QUARANTINED or SCAN_FAILED satisfies nothing (TASK-037 acceptance criterion 1).
    /// The owning module compares this with its EVIDENCE_POLICY requirement (TASK-050).
    /// </summary>
    public Task<IReadOnlySet<Guid>> GetSatisfiedEvidenceTypesAsync(BusinessTarget target, CancellationToken cancellationToken);
}
