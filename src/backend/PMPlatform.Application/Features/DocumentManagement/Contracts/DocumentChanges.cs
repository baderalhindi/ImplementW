using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>MOD-051 Edit Metadata: the whole editable set (R-5 PUT). The project and the owner never change.</summary>
public sealed record DocumentChanges(
    NarrativeText Title,
    NarrativeText? Description,
    Guid DocumentTypeItemId,
    Guid DataClassificationItemId);
