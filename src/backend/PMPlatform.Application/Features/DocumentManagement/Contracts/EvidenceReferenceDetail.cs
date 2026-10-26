using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>
/// Evidence pinned to one version. <see cref="Satisfies"/> is whether it counts towards an evidence requirement now: VALID,
/// on a link not ended, pinned to a CLEAN version (CTL-20).
/// </summary>
public sealed record EvidenceReferenceDetail(
    Guid Id,
    Guid BusinessLinkId,
    Guid DocumentId,
    Guid DocumentVersionId,
    int VersionNo,
    Guid EvidenceTypeItemId,
    Guid DesignatedByUserId,
    DateTimeOffset DesignatedAt,
    EvidenceReferenceStatus Status,
    ScanState ScanState,
    bool Satisfies);
