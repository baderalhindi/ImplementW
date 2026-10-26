using PMPlatform.Domain.Common;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>A document in a list, with its latest version's number and scan state (null before the first version commits).</summary>
public sealed record DocumentSummary(
    Guid Id,
    NarrativeText Title,
    Guid DocumentTypeItemId,
    Guid DataClassificationItemId,
    Guid? ProjectId,
    Guid OwnerUserId,
    DocumentStatus Status,
    int? LatestVersionNo,
    ScanState? LatestScanState,
    DateTimeOffset UpdatedAt);
