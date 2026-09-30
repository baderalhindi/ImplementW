using PMPlatform.Application.Features.DocumentManagement.Contracts;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement;

internal static class DocumentMapping
{
    public static DocumentDetail ToDetail(Document d, DocumentVersion? latest) => new(
        d.Id, d.Title, d.Description, d.DocumentTypeItemId, d.DataClassificationItemId, d.ProjectId, d.OwnerUserId, d.Status,
        latest is null ? null : ToDetail(latest), d.CreatedAt, d.CreatedBy, d.UpdatedAt, d.UpdatedBy);

    public static DocumentSummary ToSummary(Document d, DocumentVersion? latest) => new(
        d.Id, d.Title, d.DocumentTypeItemId, d.DataClassificationItemId, d.ProjectId, d.OwnerUserId, d.Status,
        latest?.VersionNo, latest?.ScanState, d.UpdatedAt);

    public static DocumentVersionDetail ToDetail(DocumentVersion v) => new(
        v.Id, v.DocumentId, v.VersionNo, v.FileName, v.ContentType, v.SizeBytes, v.ChecksumSha256, v.UploadedByUserId, v.UploadedAt,
        v.ScanState, v.ScanCompletedAt);

    public static BusinessLinkDetail ToDetail(BusinessLink link, IEnumerable<EvidenceReferenceDetail> evidence) => new(
        link.Id, link.DocumentId, link.LinkRole, new BusinessTarget(link.TargetModule, link.TargetType, link.TargetId),
        link.LinkedByUserId, link.LinkedAt, link.UnlinkedAt, link.UnlinkedByUserId, [.. evidence]);

    public static EvidenceReferenceDetail ToDetail(EvidenceReference e, BusinessLink link, DocumentVersion version) => new(
        e.Id, e.BusinessLinkId, link.DocumentId, e.DocumentVersionId, version.VersionNo, e.EvidenceTypeItemId, e.DesignatedByUserId, e.DesignatedAt,
        e.Status, version.ScanState, Satisfies(e, link, version));

    /// <summary>CTL-20: only VALID evidence on a link not ended, pinned to a CLEAN version, counts.</summary>
    public static bool Satisfies(EvidenceReference e, BusinessLink link, DocumentVersion version) =>
        e.Status == EvidenceReferenceStatus.Valid && link.IsActive && version.ScanState == ScanState.Clean;
}
