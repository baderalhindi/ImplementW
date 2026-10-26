using PMPlatform.Domain.Common;

namespace PMPlatform.Application.Features.DocumentManagement.Contracts;

/// <summary>MOD-050 Upload: the new document's metadata. Its first version is the uploaded file.</summary>
public sealed record DocumentDraft(
    NarrativeText Title,
    NarrativeText? Description,
    Guid DocumentTypeItemId,
    Guid DataClassificationItemId,
    Guid? ProjectId);
