using PMPlatform.Domain.Common;
using PMPlatform.Domain.DocumentManagement;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>SCR-123 Document Detail, with the latest version.</summary>
public sealed record DocumentDetail(
    Guid Id,
    NarrativeText Title,
    NarrativeText? Description,
    Guid DocumentTypeItemId,
    Guid DataClassificationItemId,
    Guid? ProjectId,
    Guid OwnerUserId,
    DocumentStatus Status,
    DocumentVersionDetail? LatestVersion,
    DateTimeOffset CreatedAt,
    Guid CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy);
